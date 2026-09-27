Imports System.Math
Imports std = System.Math

''' <summary>一组步态参数。</summary>
Public Class GaitParams

    ''' <summary>髋前后摆幅（rad）。</summary>
    Public Property Hip As Double = 0.42
    ''' <summary>膝最大屈曲（rad）。</summary>
    Public Property Knee As Double = 0.95
    ''' <summary>肩前后摆幅（rad）。</summary>
    Public Property Arm As Double = 0.30
    ''' <summary>肘基础屈曲（rad）。</summary>
    Public Property ElbowBase As Double = 0.10
    ''' <summary>肘随摆动的附加屈曲（rad）。</summary>
    Public Property Elbow As Double = 0.25
    ''' <summary>单步长度（m），决定步频 = 速度 / (2·步长)。</summary>
    Public Property StepLength As Double = 0.55
    ''' <summary>上身前倾（rad）。</summary>
    Public Property Lean As Double = 0.06

    Public Sub New()
    End Sub

    Public Sub New(hip As Double, knee As Double, arm As Double,
                   elbowBase As Double, elbow As Double,
                   stepLength As Double, lean As Double)
        Me.Hip = hip
        Me.Knee = knee
        Me.Arm = arm
        Me.ElbowBase = elbowBase
        Me.Elbow = elbow
        Me.StepLength = stepLength
        Me.Lean = lean
    End Sub
End Class

''' <summary>
''' 步态发生器：把"当前动作 + 时间相位"翻译成一帧的 <see cref="StickmanPose"/>。
''' </summary>
''' <remarks>
''' 步态采用经典的相位驱动正弦模型：
''' <list type="bullet">
''' <item><description>髋：<c>A·cos(2πp)</c>，p=0 触地（腿在前）、p=0.5 蹬离（腿在后）；</description></item>
''' <item><description>膝：<c>B·max(0, −sin(2πp))</c>，摆动中期（p≈0.75）屈曲最大，触地与蹬离时伸直；</description></item>
''' <item><description>踝：<c>髋 − 膝</c>，使脚掌在摆动过程中大致保持水平；</description></item>
''' <item><description>臂：与同侧腿反相摆动。</description></item>
''' </list>
''' 左右腿相位相差半个周期。转向通过左右腿步幅差 + 躯干扭转实现。
''' </remarks>
Public Class GaitEngine

    ''' <summary>行走速度（m/s）。</summary>
    Public Property WalkSpeed As Double = 1.0
    ''' <summary>奔跑相对行走的速度倍率。</summary>
    Public Property RunFactor As Double = 1.9
    ''' <summary>转向角速度（rad/s）。</summary>
    Public Property TurnRate As Double = 1.1
    ''' <summary>起跳初速度（m/s）。</summary>
    Public Property JumpSpeed As Double = 3.4
    ''' <summary>期望朝向（弧度，0 = +Z）。</summary>
    Public Property Heading As Double = 0.0
    ''' <summary>当前步态相位 [0,1)。</summary>
    Public Property Phase As Double = 0.0

    Private jumpPending As Boolean = False

    ''' <summary>上一帧的动作，用于检测动作切换（只在进入跳跃的那一帧给冲量）。</summary>
    Private lastAction As ActionPreset = ActionPreset.Stand

    ' ---------- 步态库 ----------
    ''' <summary>常速行走。</summary>
    Public ReadOnly Property PWalk As New GaitParams(0.28, 0.45, 0.20, 0.08, 0.15, 0.40, 0.08)
    ''' <summary>奔跑。</summary>
    Public ReadOnly Property PRun As New GaitParams(0.50, 1.10, 0.45, 0.34, 0.34, 0.75, 0.18)
    ''' <summary>高抬腿跨越。</summary>
    Public ReadOnly Property PStepOver As New GaitParams(0.54, 1.25, 0.20, 0.20, 0.18, 0.52, 0.10)
    ''' <summary>上台阶。</summary>
    Public ReadOnly Property PStairs As New GaitParams(0.60, 1.40, 0.28, 0.24, 0.24, 0.42, 0.16)
    ''' <summary>原地转向。</summary>
    Public ReadOnly Property PTurn As New GaitParams(0.24, 0.55, 0.16, 0.12, 0.16, 0.36, 0.05)

    ''' <summary>请求一次起跳（下一帧生效）。</summary>
    Public Sub RequestJump()
        jumpPending = True
    End Sub

    ''' <summary>
    ''' 重置到起始状态。
    ''' </summary>
    ''' <remarks>
    ''' 参数名不能叫 <c>heading</c>：VB 大小写不敏感，<c>Heading = heading</c> 会被解析成
    ''' 参数自赋值，朝向永远重置不成功。
    ''' </remarks>
    Public Sub Reset(Optional facing As Double = 0.0)
        Heading = facing
        Phase = 0.0
        jumpPending = False
        lastAction = ActionPreset.Stand
    End Sub

    ''' <summary>
    ''' 生成本帧的目标姿态。
    ''' </summary>
    ''' <param name="action">当前动作。</param>
    ''' <param name="pose">输出的目标姿态。</param>
    ''' <param name="dt">帧间隔。</param>
    ''' <param name="groundHeight">脚下地面高度（用于判断是否腾空）。</param>
    Public Sub Update(action As ActionPreset, pose As StickmanPose, dt As Double, Optional groundHeight As Double = 0.0)
        Dim entering As Boolean = (action <> lastAction)

        lastAction = action

        Call pose.ResetToStand()

        pose.TargetHeading = Heading

        Select Case action
            Case ActionPreset.Stand, ActionPreset.Halt
                pose.TargetSpeed = 0.0
                pose.KneeL = 0.05
                pose.KneeR = 0.05
                pose.AnkleL = -0.05
                pose.AnkleR = -0.05
                pose.PelvisHeight = 0.98

            Case ActionPreset.Walk
                pose.TargetSpeed = WalkSpeed
                Call Advance(WalkSpeed, PWalk.StepLength, dt)
                Call WalkPose(pose, PWalk)

            Case ActionPreset.Run
                pose.TargetSpeed = WalkSpeed * RunFactor
                Call Advance(pose.TargetSpeed, PRun.StepLength, dt)
                Call WalkPose(pose, PRun)

            Case ActionPreset.TurnLeft
                Heading += TurnRate * dt
                pose.TargetSpeed = WalkSpeed * 0.55
                Call Advance(pose.TargetSpeed, PTurn.StepLength, dt)
                Call WalkPose(pose, PTurn, turnDirection:=-1)

            Case ActionPreset.TurnRight
                Heading -= TurnRate * dt
                pose.TargetSpeed = WalkSpeed * 0.55
                Call Advance(pose.TargetSpeed, PTurn.StepLength, dt)
                Call WalkPose(pose, PTurn, turnDirection:=1)

            Case ActionPreset.StepOver
                pose.TargetSpeed = WalkSpeed * 0.8
                Call Advance(pose.TargetSpeed, PStepOver.StepLength, dt)
                Call WalkPose(pose, PStepOver)
                pose.PelvisHeight = 1.02

            Case ActionPreset.ClimbStairs
                pose.TargetSpeed = WalkSpeed * 0.62
                Call Advance(pose.TargetSpeed, PStairs.StepLength, dt)
                Call WalkPose(pose, PStairs)
                pose.PelvisHeight = 1.02

            Case ActionPreset.Jump
                ' 只在"刚进入"跳跃动作的那一帧给一次起跳冲量；
                ' 若每帧都触发，3.4 m/s 的初速度会叠加到 30 m/s（MaxSpeed 上限），
                ' 角色直接飞出场景。
                If entering Then
                    jumpPending = True
                End If

                pose.TargetSpeed = WalkSpeed * 0.7
                Call Advance(pose.TargetSpeed, PWalk.StepLength, dt)
                Call WalkPose(pose, PWalk)
        End Select

        ' 头部保持水平：抵消上身前倾
        pose.NeckPitch = -pose.SpinePitch

        If jumpPending Then
            pose.JumpSpeed = JumpSpeed
            jumpPending = False
        End If
    End Sub

    Private Sub Advance(speed As Double, stepLength As Double, dt As Double)
        Dim freq As Double = speed / std.Max(2.0 * stepLength, 0.05)

        Phase += freq * dt
        Phase = Phase - std.Floor(Phase)
    End Sub

    ''' <summary>
    ''' 由相位生成双腿 / 双臂的关节角度。
    ''' </summary>
    ''' <param name="turnDirection">
    ''' 0 = 直行；−1 = 左转（右腿步幅更大）；+1 = 右转（左腿步幅更大）。
    ''' </param>
    Private Sub WalkPose(pose As StickmanPose, p As GaitParams, Optional turnDirection As Integer = 0)
        Dim lp As Double = 2.0 * std.PI * Phase
        Dim rp As Double = 2.0 * std.PI * (Phase + 0.5)
        Dim scaleL As Double = 1.0
        Dim scaleR As Double = 1.0

        If turnDirection < 0 Then
            scaleL = 0.65
            scaleR = 1.35
        ElseIf turnDirection > 0 Then
            scaleL = 1.35
            scaleR = 0.65
        End If

        pose.HipPitchL = p.Hip * std.Cos(lp) * scaleL
        pose.HipPitchR = p.Hip * std.Cos(rp) * scaleR
        pose.KneeL = p.Knee * std.Max(0.0, -std.Sin(lp)) * scaleL
        pose.KneeR = p.Knee * std.Max(0.0, -std.Sin(rp)) * scaleR

        ' 踝关节跟随髋膝，使脚掌保持水平
        pose.AnkleL = pose.HipPitchL - pose.KneeL
        pose.AnkleR = pose.HipPitchR - pose.KneeR

        ' 手臂与同侧腿反相
        pose.ShoulderPitchL = -p.Arm * std.Cos(lp) * scaleL
        pose.ShoulderPitchR = -p.Arm * std.Cos(rp) * scaleR
        pose.ElbowL = p.ElbowBase + p.Elbow * std.Max(0.0, std.Cos(lp))
        pose.ElbowR = p.ElbowBase + p.Elbow * std.Max(0.0, std.Cos(rp))

        pose.SpinePitch = p.Lean

        ' 转向时上身朝转向侧轻微侧倾
        If turnDirection <> 0 Then
            pose.SpineRoll = -turnDirection * 0.06
        End If
    End Sub
End Class
