Imports System.Math
Imports std = System.Math

''' <summary>
''' 果蝇大脑的<strong>感觉通道语义</strong>与一帧感觉输入。
''' </summary>
''' <remarks>
''' 通道布局刻意与贪吃蛇 demo（<c>FlywireSnake.SnakeSensors</c>）的 16 通道保持同构，
''' 这样 <see cref="FlyBrain"/> 可以原样照搬 <c>SnakeBrain</c> 的电流注入方式：
''' 每个通道的强度 [0,1] 乘以 <c>SensorCurrent</c> 后均摊到该通道的一组
''' afferent（感觉）神经元上。
''' <code>
'''  0.. 7   任务目标方位：以身体朝向为参考系的 8 个扇区，强度 = 距离衰减
'''      8   前方障碍接近度（跨越障碍的目标）
'''      9   前方地面抬升（台阶边缘）
'''     10   侧向偏移（偏离走道中心线）
'''     11   失稳 / 跌倒标志
'''  12,13   左 / 右足触地
'''     14   水平速度
'''     15   躯干倾角
''' </code>
''' </remarks>
Public NotInheritable Class FlyChannels

    ''' <summary>目标方位扇区数。</summary>
    Public Const TargetSectors As Integer = 8

    ''' <summary>危险 / 本体感觉通道数。</summary>
    Public Const ProprioChannels As Integer = 8

    ''' <summary>感觉通道总数。</summary>
    Public Const ChannelCount As Integer = TargetSectors + ProprioChannels

    ' ---- 通道下标 ----
    Public Const ChTargetSector0 As Integer = 0
    Public Const ChObstacleAhead As Integer = 8
    Public Const ChStepAhead As Integer = 9
    Public Const ChLateralOffset As Integer = 10
    Public Const ChUnstable As Integer = 11
    Public Const ChFootL As Integer = 12
    Public Const ChFootR As Integer = 13
    Public Const ChSpeed As Integer = 14
    Public Const ChTilt As Integer = 15

    ''' <summary>目标方位的感知半径（m），超过它强度为 0。</summary>
    Public Const TargetSenseRadius As Double = 24.0

    ''' <summary>障碍的感知距离（m）。</summary>
    Public Const ObstacleSenseRange As Double = 2.0

    Private Sub New()
    End Sub
End Class

''' <summary>
''' 一帧感觉输入（<see cref="FlyChannels.ChannelCount"/> 个 [0,1] 强度）。
''' </summary>
Public Structure FlySensorFrame

    ''' <summary>通道强度，长度 = <see cref="FlyChannels.ChannelCount"/>。</summary>
    Public Values As Double()

    ''' <summary>本帧选定的任务目标点（世界坐标，调试 / 可视化用）。</summary>
    Public Target As Vec3

    ''' <summary>目标的方位角（弧度，0 = 正前方，正值 = 偏右）。</summary>
    Public TargetBearing As Double

    ''' <summary>通道总数。</summary>
    Public ReadOnly Property Length As Integer
        Get
            Return If(Values Is Nothing, 0, Values.Length)
        End Get
    End Property
End Structure

''' <summary>
''' 把火柴人的世界状态编码成果蝇大脑的感觉通道强度。
''' </summary>
''' <remarks>
''' 对应贪吃蛇 demo 里的 <c>FlywireSnake.SnakeSensorEncoder.Encode(game)</c>，
''' 只不过读取的是 <see cref="FigureEnvironment"/> 而不是 <c>Snake2.Game</c>。
''' </remarks>
Public NotInheritable Class FlySensorEncoder

    Private Sub New()
    End Sub

    ''' <summary>
    ''' 编码一帧感觉输入。
    ''' </summary>
    Public Shared Function Encode(env As FigureEnvironment) As FlySensorFrame
        Dim values As Double() = New Double(FlyChannels.ChannelCount - 1) {}
        Dim frame As New FlySensorFrame With {.Values = values}

        If env Is Nothing Then
            Return frame
        End If

        Dim body As Vec3 = env.Position
        Dim fwd As Vec3 = env.Skeleton.BodyForward

        If fwd.LengthSquared < 1.0E-6 Then
            fwd = New Vec3(1, 0, 0)
        End If

        fwd = New Vec3(fwd.X, 0, fwd.Z).Normalize()

        If fwd.LengthSquared < 1.0E-6 Then
            fwd = New Vec3(1, 0, 0)
        End If

        Dim right As Vec3 = Vec3.Cross(New Vec3(0, 1, 0), fwd).Normalize()

        ' ---------------- 0..7 任务目标方位扇区 ----------------
        Dim target As Vec3 = NextTarget(env, body)
        Dim delta As Vec3 = target - body
        Dim flat As New Vec3(delta.X, 0, delta.Z)
        Dim distance As Double = flat.Length
        Dim bearing As Double = 0.0

        If distance > 1.0E-6 Then
            Dim dir As Vec3 = flat / distance
            ' 正值 = 目标在身体右侧
            bearing = std.Atan2(Vec3.Dot(dir, right), Vec3.Dot(dir, fwd))
        End If

        frame.Target = target
        frame.TargetBearing = bearing

        ' bearing ∈ [-π, π]，等分成 8 个扇区：扇区 0 = 正前方，每扇区 45°
        Dim sector As Double = bearing / (2.0 * std.PI / FlyChannels.TargetSectors)
        Dim index As Integer = CInt(std.Floor(sector + 0.5)) Mod FlyChannels.TargetSectors

        If index < 0 Then
            index += FlyChannels.TargetSectors
        End If

        ' 距离衰减：越近越强
        Dim proximity As Double = Clamp01(1.0 - distance / FlyChannels.TargetSenseRadius)

        ' 正前方的扇区给最强信号，相邻扇区给一半（软扇区，避免目标恰好在分界线上抖动）
        values(FlyChannels.ChTargetSector0 + index) = proximity
        values(FlyChannels.ChTargetSector0 + (index + 1) Mod FlyChannels.TargetSectors) = proximity * 0.5
        values(FlyChannels.ChTargetSector0 + (index + FlyChannels.TargetSectors - 1) Mod FlyChannels.TargetSectors) =
            proximity * 0.5

        ' ---------------- 8 前方障碍接近度 ----------------
        values(FlyChannels.ChObstacleAhead) = ObstacleProximity(env, body, fwd)

        ' ---------------- 9 前方地面抬升（台阶） ----------------
        values(FlyChannels.ChStepAhead) = StepAhead(env, body, fwd)

        ' ---------------- 10 侧向偏移 ----------------
        values(FlyChannels.ChLateralOffset) = Clamp01(std.Abs(body.Z) / 2.0)

        ' ---------------- 11 失稳 / 跌倒 ----------------
        Dim tilt As Double = env.Skeleton.TiltAngle

        values(FlyChannels.ChUnstable) = Clamp01(std.Max(tilt / 0.6, If(env.IsFallen, 1.0, 0.0)))

        ' ---------------- 12,13 足部触地 ----------------
        Dim contact As (Left As Boolean, Right As Boolean) = env.Skeleton.FootContact

        values(FlyChannels.ChFootL) = If(contact.Left, 1.0, 0.0)
        values(FlyChannels.ChFootR) = If(contact.Right, 1.0, 0.0)

        ' ---------------- 14 水平速度 ----------------
        Dim v As Vec3 = Vec3.FromPhysics(env.Skeleton.Bodies(BoneIndex.Pelvis).Velocity)
        Dim speed As Double = New Vec3(v.X, 0, v.Z).Length

        values(FlyChannels.ChSpeed) = Clamp01(speed / 2.0)

        ' ---------------- 15 躯干倾角 ----------------
        values(FlyChannels.ChTilt) = Clamp01(tilt / 0.6)

        For i As Integer = 0 To values.Length - 1
            values(i) = Clamp01(values(i))
        Next

        Return frame
    End Function

    ''' <summary>
    ''' 下一个任务目标点：障碍 → 台阶 → 台阶顶平台远端。
    ''' </summary>
    Public Shared Function NextTarget(env As FigureEnvironment, body As Vec3) As Vec3
        Dim level As Level = env.Level

        If body.X < level.ObstacleX - 0.5 Then
            Return New Vec3(level.ObstacleX, 0, 0)
        ElseIf body.X < level.StairsStartX - 0.5 Then
            Return New Vec3(level.StairsStartX, 0, 0)
        Else
            Return New Vec3(level.StairsStartX + 5.0, 0, 0)
        End If
    End Function

    ''' <summary>
    ''' 沿身体朝向方向、障碍横杆的接近度。
    ''' </summary>
    Public Shared Function ObstacleProximity(env As FigureEnvironment, body As Vec3, fwd As Vec3) As Double
        Dim level As Level = env.Level
        Dim along As Double = Vec3.Dot(fwd, New Vec3(1, 0, 0))

        ' 朝向分量太小（侧对障碍）时给一个弱信号，避免除零
        If std.Abs(along) < 0.15 Then
            Return 0.0
        End If

        Dim distance As Double = (level.ObstacleX - body.X) / along

        ' 只感知"在前方"的障碍
        If distance < 0.0 OrElse distance > FlyChannels.ObstacleSenseRange Then
            Return 0.0
        End If

        Return Clamp01(1.0 - distance / FlyChannels.ObstacleSenseRange)
    End Function

    ''' <summary>
    ''' 身前半步远处的地面相对脚下地面的抬升量（台阶边缘）。
    ''' </summary>
    Public Shared Function StepAhead(env As FigureEnvironment, body As Vec3, fwd As Vec3) As Double
        Dim level As Level = env.Level
        Dim probe As Vec3 = body + fwd * 0.45
        Dim here As Double = level.GroundHeightAt(body.X, body.Z)
        Dim ahead As Double = level.GroundHeightAt(probe.X, probe.Z)
        Dim rise As Double = ahead - here

        If rise <= 0.0 Then
            Return 0.0
        End If

        Return Clamp01(rise / 0.2)
    End Function

    Private Shared Function Clamp01(v As Double) As Double
        If Double.IsNaN(v) OrElse Double.IsInfinity(v) Then
            Return 0.0
        End If

        Return std.Min(1.0, std.Max(0.0, v))
    End Function
End Class
