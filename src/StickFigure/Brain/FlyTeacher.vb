Imports System.Math
Imports std = System.Math

''' <summary>
''' 规则教师的一帧输出：期望的关节偏置 + 速度/转向偏置 + 起跳请求。
''' </summary>
Public Structure FlyTeacherResult

    ''' <summary>16 维关节偏置（弧度）：期望动作姿态 − 基准 Walk 姿态。</summary>
    Public Offsets As Double()

    ''' <summary>速度偏置（m/s）：期望动作的行走速度 − 基准 Walk 速度。</summary>
    Public SpeedBias As Double

    ''' <summary>转向偏置（[-1,1]）：正值 = 向右转。</summary>
    Public TurnBias As Double

    ''' <summary>&gt; 0 时本帧要给一次起跳冲量（跨越障碍的跨栏步）。</summary>
    Public HopSpeed As Double

    ''' <summary>教师选定的期望动作（诊断 / 状态条显示用）。</summary>
    Public Intent As ActionPreset

    ''' <summary>选定期望动作的理由（诊断用）。</summary>
    Public Reason As String
End Structure

''' <summary>
''' 规则教师：看火柴人的世界状态选出"现在应该做什么"，并给出<b>连续的</b>教学信号。
''' </summary>
''' <remarks>
''' <para>
''' 对应贪吃蛇 demo 里的 <c>FlywireSnake.SnakeSensorEncoder.TeacherAction(game)</c>，
''' 但输出不是离散动作而是 <b>16 维关节偏置 + 速度/转向偏置</b>——
''' 因为读出层是回归器（<see cref="FlyRegressor"/>），教师信号必须是连续的。
''' </para>
''' <para>
''' 教学信号的构造方式：维护两台影子步态引擎——
''' <list type="bullet">
''' <item><description><see cref="m_mirror"/>：与主步态同相位、同朝向地跑 <b>Walk</b>（= 大脑什么都不做时的基准姿态）；</description></item>
''' <item><description><see cref="m_shadow"/>：跑教师选定的期望动作（= 教师希望角色摆出的姿态）。</description></item>
''' </list>
''' 两者的姿态逐字段求差就是"为了做出这个动作需要叠加多少关节偏置"，
''' 它天然平滑、有界、而且与基准步态强相关 —— 正是线性回归能学的东西。
''' 当期望动作就是 Walk 时两者相减为零，角色直行。
''' </para>
''' </remarks>
Public Class FlyTeacher

    ''' <summary>识别到障碍就开始跨越的距离（m）。</summary>
    Public Property ObstacleTrigger As Double = 0.62

    ''' <summary>识别到台阶边缘就开始攀爬的抬升量（m）。</summary>
    Public Property StepTrigger As Double = 0.45

    ''' <summary>朝向偏差超过这个角度就开始转向（弧度）。</summary>
    Public Property TurnTrigger As Double = 0.28

    Private ReadOnly m_mirror As GaitEngine
    Private ReadOnly m_shadow As GaitEngine

    Private ReadOnly m_mirrorPose As New StickmanPose()
    Private ReadOnly m_shadowPose As New StickmanPose()

    Public Sub New(baseGait As GaitEngine)
        m_mirror = New GaitEngine()
        m_shadow = New GaitEngine()

        Call SyncFrom(baseGait)
    End Sub

    ''' <summary>把两台影子引擎的相位 / 朝向同步到主步态（每帧调用）。</summary>
    Public Sub SyncFrom(baseGait As GaitEngine)
        If baseGait Is Nothing Then Return

        m_mirror.Phase = baseGait.Phase
        m_mirror.Heading = baseGait.Heading
        m_mirror.WalkSpeed = baseGait.WalkSpeed

        m_shadow.Phase = baseGait.Phase
        m_shadow.Heading = baseGait.Heading
        m_shadow.WalkSpeed = baseGait.WalkSpeed
    End Sub

    ''' <summary>
    ''' 产生一帧教学信号。
    ''' </summary>
    Public Function Target(env As FigureEnvironment,
                           sensors As FlySensorFrame,
                           baseGait As GaitEngine,
                           dt As Double) As FlyTeacherResult

        Call SyncFrom(baseGait)

        Dim ground As Double = env.Level.GroundHeightAt(env.Position.X, env.Position.Z)
        Dim result As New FlyTeacherResult With {
            .Offsets = New Double() {},
            .Intent = ActionPreset.Walk,
            .Reason = "cruise",
            .HopSpeed = 0.0,
            .SpeedBias = 0.0,
            .TurnBias = 0.0
        }

        ' ---------- 1) 选定期望动作 ----------
        Dim intent As ActionPreset = ActionPreset.Walk
        Dim reason As String = "cruise"
        Dim obstacleDist As Double = ObstacleDistance(env)

        If env.IsFallen Then
            intent = ActionPreset.Stand
            reason = "fallen"
        ElseIf obstacleDist <= ObstacleTrigger Then
            intent = ActionPreset.StepOver
            reason = $"obstacle {obstacleDist:F2}m"
        ElseIf sensors.Values(FlyChannels.ChStepAhead) > 0.35 AndAlso AheadRise(env) >= StepTrigger Then
            intent = ActionPreset.ClimbStairs
            reason = "step ahead"
        Else
            Dim bearing As Double = sensors.TargetBearing

            If std.Abs(bearing) > TurnTrigger Then
                intent = If(bearing > 0, ActionPreset.TurnRight, ActionPreset.TurnLeft)
                reason = $"heading {bearing * 180 / std.PI:F0}°"
            ElseIf sensors.Values(FlyChannels.ChTargetSector0) < 0.25 Then
                intent = ActionPreset.Run
                reason = "target far"
            End If
        End If

        ' ---------- 2) 两台影子步态各推进一步 ----------
        Call m_mirror.Update(ActionPreset.Walk, m_mirrorPose, dt, ground)

        If intent = ActionPreset.StepOver AndAlso obstacleDist <= ObstacleTrigger * 0.75 Then
            ' 跨栏步：贴近障碍时给一次起跳冲量
            ' （影子引擎自己的 entering 检测会被"上一帧也是 StepOver"挡掉，所以这里手动给）
            m_shadow.JumpSpeed = env.Gait.StepOverJumpSpeed
        End If

        Call m_shadow.Update(intent, m_shadowPose, dt, ground)

        ' ---------- 3) 目标姿态 − 基准姿态 = 关节偏置 ----------
        Dim offsets(15) As Double

        offsets(0) = m_shadowPose.HipPitchL - m_mirrorPose.HipPitchL
        offsets(1) = m_shadowPose.HipPitchR - m_mirrorPose.HipPitchR
        offsets(2) = m_shadowPose.KneeL - m_mirrorPose.KneeL
        offsets(3) = m_shadowPose.KneeR - m_mirrorPose.KneeR
        offsets(4) = m_shadowPose.AnkleL - m_mirrorPose.AnkleL
        offsets(5) = m_shadowPose.AnkleR - m_mirrorPose.AnkleR
        offsets(6) = m_shadowPose.HipRollL - m_mirrorPose.HipRollL
        offsets(7) = m_shadowPose.HipRollR - m_mirrorPose.HipRollR
        offsets(8) = m_shadowPose.ShoulderPitchL - m_mirrorPose.ShoulderPitchL
        offsets(9) = m_shadowPose.ShoulderPitchR - m_mirrorPose.ShoulderPitchR
        offsets(10) = m_shadowPose.ElbowL - m_mirrorPose.ElbowL
        offsets(11) = m_shadowPose.ElbowR - m_mirrorPose.ElbowR
        offsets(12) = m_shadowPose.SpinePitch - m_mirrorPose.SpinePitch
        offsets(13) = m_shadowPose.SpineRoll - m_mirrorPose.SpineRoll
        offsets(14) = m_shadowPose.SpineYaw - m_mirrorPose.SpineYaw
        offsets(15) = m_shadowPose.NeckYaw - m_mirrorPose.NeckYaw

        For i As Integer = 0 To offsets.Length - 1
            offsets(i) = Clamp(offsets(i), FigureEnvironment.OffsetLimit)
        Next

        ' ---------- 4) 速度 / 转向偏置 ----------
        Dim speedBias As Double = m_shadowPose.TargetSpeed - m_mirrorPose.TargetSpeed
        Dim turnBias As Double = If(env.IsFallen, 0.0, Clamp(sensors.TargetBearing / 0.8, -1.0, 1.0))

        result.Offsets = offsets
        result.SpeedBias = Clamp(speedBias, -1.5, 1.5)

        result.TurnBias = turnBias
        result.HopSpeed = If(m_shadowPose.JumpSpeed > 0, m_shadowPose.JumpSpeed, 0.0)
        result.Intent = intent
        result.Reason = reason

        Return result
    End Function

    ''' <summary>沿身体朝向到障碍横杆的距离（m）；不在前方时返回 +∞。</summary>
    Private Shared Function ObstacleDistance(env As FigureEnvironment) As Double
        Dim fwd As Vec3 = env.Skeleton.BodyForward
        Dim along As Double = Vec3.Dot(fwd, New Vec3(1, 0, 0))

        If std.Abs(along) < 0.15 Then
            Return Double.PositiveInfinity
        End If

        Dim distance As Double = (env.Level.ObstacleX - env.Position.X) / along

        Return If(distance >= 0, distance, Double.PositiveInfinity)
    End Function

    ''' <summary>身前半步远处的地面抬升量（m）。</summary>
    Private Shared Function AheadRise(env As FigureEnvironment) As Double
        Dim fwd As Vec3 = env.Skeleton.BodyForward
        Dim probe As Vec3 = env.Position + fwd * 0.45

        Return env.Level.GroundHeightAt(probe.X, probe.Z) - env.Level.GroundHeightAt(env.Position.X, env.Position.Z)
    End Function

    Private Shared Function Clamp(v As Double, limit As Double) As Double
        Return Clamp(v, -limit, limit)
    End Function

    Private Shared Function Clamp(v As Double, lo As Double, hi As Double) As Double
        If Double.IsNaN(v) OrElse Double.IsInfinity(v) Then
            Return 0.0
        End If

        Return std.Min(hi, std.Max(lo, v))
    End Function

End Class
