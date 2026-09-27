Imports System.Math
Imports Microsoft.VisualBasic.Imaging.Physics
Imports Microsoft.VisualBasic.Imaging.Physics.Joints3D
Imports Microsoft.VisualBasic.Imaging.Physics.Math3D
Imports ImagingBitmap = Microsoft.VisualBasic.Imaging.Bitmap
Imports std = System.Math

''' <summary>
''' 火柴人仿真环境的门面：把物理世界、骨架、步态、平衡、动作调度与双视角渲染
''' 组装成一个可以"按帧推进"的环境。
''' </summary>
''' <remarks>
''' 本类<strong>不依赖任何 WinForms 类型</strong>（唯一用到的是 DirectX 的离屏画布，
''' 且取帧失败时安全降级），因此可以直接在训练循环里：
''' <code>
''' Dim env As New FigureEnvironment()
''' Do
'''     env.Act(ActionPreset.Walk)
'''     env.Step(1 / 60)
'''     Dim obs = env.GetObservation()
'''     Dim action = agent.Act(obs)
'''     env.SetAction(action)
''' Loop
''' </code>
''' </remarks>
Public Class FigureEnvironment

    ''' <summary>3D 物理世界。</summary>
    Public ReadOnly Property World As New PhysicsWorld3D()

    ''' <summary>关卡（地面 / 障碍 / 台阶）。</summary>
    Public Property Level As Level

    ''' <summary>火柴人骨架。</summary>
    Public ReadOnly Property Skeleton As New StickmanSkeleton()

    ''' <summary>步态发生器。</summary>
    Public ReadOnly Property Gait As New GaitEngine()

    ''' <summary>平衡控制器。</summary>
    Public ReadOnly Property Balance As New BalanceController()

    ''' <summary>动作调度器。</summary>
    Public ReadOnly Property Director As New ActionDirector()

    ''' <summary>头部视角渲染器（第一人称画面）。</summary>
    Public ReadOnly Property Head As New HeadViewRenderer()

    ''' <summary>当前目标姿态（每帧由步态引擎刷新）。</summary>
    Public ReadOnly Property Pose As New StickmanPose()

    ''' <summary>外部策略模型；为 <see cref="NullAgent"/> 时完全由预设脚本驱动。</summary>
    Public Property Agent As IStickmanAgent = New NullAgent()

    ''' <summary>仿真时间（秒）。</summary>
    Public Property Time As Double = 0.0

    ''' <summary>跌倒后自动复位的等待时间（秒）；&lt;=0 表示不自动复位。</summary>
    Public Property FallResetDelay As Double = 1.6

    ''' <summary>取视觉帧的默认分辨率。</summary>
    Public Property VisionWidth As Integer = 160
    ''' <summary>取视觉帧的默认分辨率。</summary>
    Public Property VisionHeight As Integer = 120

    ''' <summary>头部相机上向量向世界竖直方向混合的比例（抑制画面抖动）。</summary>
    Public Property HeadUpBlend As Double = 0.6

    ''' <summary>上一帧的物理耗时（毫秒），用于观测性能。</summary>
    Public Property LastStepMs As Double = 0.0

    Private fallTimer As Double = 0.0

    Public Sub New(Optional gravity As Double = -9.81)
        World.Gravity = New Vector3(0, gravity, 0)
        World.FixedDt = 1.0 / 60.0
        ' 子步 6 个 → dt = 1/360 s：主动布娃娃的马达刚度较高，需要更小的积分步长
        World.Substeps = 6
        World.Iterations = 18
        ' 位置修正强度按子步长折算：bias = β/dt·穿透，子步 5 个时 dt=1/300，
        ' 若沿用 2D 世界的 β=0.2 会给出 6 m/s 的弹开速度，把角色震散。
        ' 这里取 0.05，等效于 60Hz 单步下的 0.2。
        World.Baumgarte = 0.05
        World.Slop = 0.008

        Level = LevelBuilder.BuildDefault(World)
        Skeleton.Build(World, Level.StartPosition, Level.StartHeading)
        Gait.Reset(Level.StartHeading)
        Pose.ResetToStand()

        Call Skeleton.ApplyPose(Pose)
        Call SyncHeadCamera()
    End Sub

    ' /********************************************************************************/
    '  主循环
    ' /********************************************************************************/

    ''' <summary>
    ''' 复位到出生点。
    ''' </summary>
    Public Sub Reset(Optional position As Vec3? = Nothing, Optional heading As Double = Double.NaN)
        Dim spawn As Vec3 = If(position.HasValue, position.Value, Level.StartPosition)
        Dim facing As Double = If(Double.IsNaN(heading), Level.StartHeading, heading)

        Call World.ClearVelocities()
        Call Skeleton.Reset(spawn, facing)
        Call Gait.Reset(facing)
        Call Pose.ResetToStand()
        Call Skeleton.ApplyPose(Pose)
        Call Director.SetAction(ActionPreset.Stand, -1, ActionPreset.Stand)

        Time = 0.0
        fallTimer = 0.0

        Call SyncHeadCamera()
    End Sub

    ''' <summary>
    ''' 推进一帧。
    ''' </summary>
    ''' <param name="dt">帧间隔（秒）。</param>
    Public Sub [Step](dt As Double)
        Dim watch As Stopwatch = Stopwatch.StartNew()

        Time += dt

        Dim pelvis As Vec3 = Skeleton.PelvisPosition
        Dim groundHeight As Double = Level.GroundHeightAt(pelvis.X, pelvis.Z)
        Dim action As ActionPreset = Director.Update(dt, pelvis.X)

        ' 1. 步态生成目标姿态
        Call Gait.Update(action, Pose, dt, groundHeight)

        ' 2. 外部策略可以在步态之上叠加关节角度偏置 / 选择离散动作
        If Agent IsNot Nothing AndAlso TypeOf Agent IsNot NullAgent Then
            Dim hint As Integer = Agent.SelectAction(GetObservation(withVision:=False))

            If hint >= 0 Then
                Call Act(hint)
            Else
                Call SetAction(Agent.Act(GetObservation(withVision:=False)))
            End If
        End If

        ' 3. 写入马达（马达扭矩会在 world.Step 的每个子步开头施加）
        Call Skeleton.ApplyPose(Pose)

        ' 4. 平衡反射
        Call Balance.Apply(Skeleton, Pose, groundHeight, dt)

        ' 5. 起跳
        If Pose.JumpSpeed > 0 Then
            Call Skeleton.ApplyJump(Pose.JumpSpeed)
            Pose.JumpSpeed = 0.0
        End If

        ' 6. 物理推进
        Call World.Step(dt)

        ' 7. 跌倒检测与自动复位
        If FallResetDelay > 0 Then
            If Skeleton.IsFallen Then
                fallTimer += dt

                If fallTimer >= FallResetDelay Then
                    Call Reset()
                End If
            Else
                fallTimer = 0.0
            End If
        End If

        Call SyncHeadCamera()

        watch.Stop()
        LastStepMs = watch.Elapsed.TotalMilliseconds
    End Sub

    ' /********************************************************************************/
    '  离散 / 连续控制接口
    ' /********************************************************************************/

    ''' <summary>
    ''' 离散控制：直接触发一个预设动作（DQN 这类离散策略用）。
    ''' </summary>
    Public Sub Act(actionIndex As Integer)
        Dim action As ActionPreset

        If System.Enum.IsDefined(GetType(ActionPreset), actionIndex) Then
            action = CType(actionIndex, ActionPreset)
        Else
            action = ActionPreset.Stand
        End If

        Call Act(action)
    End Sub

    ''' <summary>离散控制：直接触发一个预设动作。</summary>
    Public Sub Act(action As ActionPreset)
        Call Director.SetAction(action, -1.0, ActionPreset.Stand)

        If action = ActionPreset.Jump Then
            Call Gait.RequestJump()
        End If
    End Sub

    ''' <summary>
    ''' 连续控制：把外部模型输出的动作向量叠加到当前目标姿态上。
    ''' </summary>
    ''' <param name="action">长度不超过 <see cref="StickmanPose.ActionDimension"/> 的向量。</param>
    Public Sub SetAction(action As Single())
        If action Is Nothing OrElse action.Length = 0 Then
            Return
        End If

        ' 动作向量是"叠加在步态之上的残差偏置"，而不是替换整个步态，
        ' 这样外部模型只需学习残差就能驱动角色。
        Dim v As Double() = action.Select(Function(x) CDbl(x)).ToArray()
        Const limit As Double = 1.2

        If v.Length > 0 Then Pose.HipPitchL += ClampSym(v(0), limit)
        If v.Length > 1 Then Pose.HipPitchR += ClampSym(v(1), limit)
        If v.Length > 2 Then Pose.KneeL += ClampSym(v(2), limit)
        If v.Length > 3 Then Pose.KneeR += ClampSym(v(3), limit)
        If v.Length > 4 Then Pose.AnkleL += ClampSym(v(4), limit)
        If v.Length > 5 Then Pose.AnkleR += ClampSym(v(5), limit)
        If v.Length > 6 Then Pose.HipRollL += ClampSym(v(6), limit)
        If v.Length > 7 Then Pose.HipRollR += ClampSym(v(7), limit)
        If v.Length > 8 Then Pose.ShoulderPitchL += ClampSym(v(8), limit)
        If v.Length > 9 Then Pose.ShoulderPitchR += ClampSym(v(9), limit)
        If v.Length > 10 Then Pose.ElbowL += ClampSym(v(10), limit)
        If v.Length > 11 Then Pose.ElbowR += ClampSym(v(11), limit)
        If v.Length > 12 Then Pose.SpinePitch += ClampSym(v(12), limit)
        If v.Length > 13 Then Pose.SpineRoll += ClampSym(v(13), limit)
        If v.Length > 14 Then Pose.SpineYaw += ClampSym(v(14), limit)
        If v.Length > 15 Then Pose.NeckYaw += ClampSym(v(15), limit)

        Call Skeleton.ApplyPose(Pose)
    End Sub

    Private Shared Function ClampSym(v As Double, limit As Double) As Double
        If Double.IsNaN(v) OrElse Double.IsInfinity(v) Then
            Return 0.0
        End If

        Return std.Min(limit, std.Max(-limit, v))
    End Function

    ''' <summary>
    ''' 连续控制：把动作向量写入姿态（替换而非叠加）。模型希望完全接管时使用。
    ''' </summary>
    Public Sub SetActionAbsolute(action As Single())
        If action Is Nothing OrElse action.Length = 0 Then
            Return
        End If

        Call Pose.FromActionVector(action)
        Call Skeleton.ApplyPose(Pose)
    End Sub

    ' /********************************************************************************/
    '  观测接口
    ' /********************************************************************************/

    ''' <summary>
    ''' 取一帧观测。
    ''' </summary>
    ''' <param name="withVision">是否同时抓取头部视角的灰度帧（有 GPU 开销）。</param>
    Public Function GetObservation(Optional withVision As Boolean = False) As Observation
        Dim pelvis As Vec3 = Skeleton.PelvisPosition
        Dim up As Vec3 = Skeleton.TorsoUp
        Dim fwd As Vec3 = Skeleton.BodyForward
        Dim right As Vec3 = Vec3.Cross(fwd, up)

        If right.LengthSquared < 1.0E-8 Then
            right = New Vec3(1, 0, 0)
        End If

        right = right.Normalize()

        Dim count As Integer = System.Enum.GetValues(GetType(BoneIndex)).Length
        Dim local(count * 3 - 1) As Double
        Dim vel(count * 3 - 1) As Double
        Dim i As Integer = 0

        For Each index As BoneIndex In System.Enum.GetValues(GetType(BoneIndex)).Cast(Of BoneIndex)()
            Dim body As RigidBody3D = Skeleton.Bodies(index)
            Dim d As Vec3 = Vec3.FromPhysics(body.Position) - pelvis
            Dim v As Vec3 = Vec3.FromPhysics(body.Velocity)

            local(i * 3) = Vec3.Dot(d, right)
            local(i * 3 + 1) = Vec3.Dot(d, up)
            local(i * 3 + 2) = Vec3.Dot(d, fwd)

            vel(i * 3) = Vec3.Dot(v, right)
            vel(i * 3 + 1) = Vec3.Dot(v, up)
            vel(i * 3 + 2) = Vec3.Dot(v, fwd)

            i += 1
        Next

        Dim jointCount As Integer = System.Enum.GetValues(GetType(JointIndex)).Length
        Dim errors(jointCount - 1) As Double
        Dim j As Integer = 0

        For Each index As JointIndex In System.Enum.GetValues(GetType(JointIndex)).Cast(Of JointIndex)()
            errors(j) = Skeleton.Motors(index).AngleError
            j += 1
        Next

        Dim contact As (Left As Boolean, Right As Boolean) = Skeleton.FootContact
        Dim desired As Vec3 = Level.HeadingToForward(Pose.TargetHeading)
        Dim headingErr As Double = std.Atan2(fwd.Z * desired.X - fwd.X * desired.Z, Vec3.Dot(fwd, desired))
        Dim obs As New Observation With {
            .Timestamp = Time,
            .JointLocal = local,
            .JointVelocity = vel,
            .TorsoUp = {up.X, up.Y, up.Z},
            .TorsoForward = {fwd.X, fwd.Y, fwd.Z},
            .JointAngleError = errors,
            .PelvisHeight = pelvis.Y - Level.GroundHeightAt(pelvis.X, pelvis.Z),
            .TiltAngle = Skeleton.TiltAngle,
            .HeadingError = headingErr,
            .Speed = New Vec3(Vec3.FromPhysics(Skeleton.Bodies(BoneIndex.Pelvis).Velocity).X, 0,
                              Vec3.FromPhysics(Skeleton.Bodies(BoneIndex.Pelvis).Velocity).Z).Length,
            .FootContact = {If(contact.Left, 1.0, 0.0), If(contact.Right, 1.0, 0.0)},
            .IsFallen = Skeleton.IsFallen,
            .Vision = New Byte() {},
            .VisionWidth = 0,
            .VisionHeight = 0
        }

        If withVision Then
            Try
                obs.Vision = GetVisionGray(VisionWidth, VisionHeight)
                obs.VisionWidth = VisionWidth
                obs.VisionHeight = VisionHeight
            Catch ex As Exception
                obs.Vision = New Byte() {}
            End Try
        End If

        Return obs
    End Function

    ' /********************************************************************************/
    '  视觉接口
    ' /********************************************************************************/

    ''' <summary>取头部视角画面（分辨率可配）。</summary>
    Public Function GetVisionFrame(Optional width As Integer = -1, Optional height As Integer = -1) As ImagingBitmap
        If width <= 0 Then width = VisionWidth
        If height <= 0 Then height = VisionHeight

        Try
            Return Head.Capture(width, height, Level.Mesh, FigureLines())
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>取头部视角的灰度张量（行优先，长度 = w·h，0..255）。</summary>
    Public Function GetVisionGray(Optional width As Integer = -1, Optional height As Integer = -1) As Byte()
        If width <= 0 Then width = VisionWidth
        If height <= 0 Then height = VisionHeight

        Try
            Return Head.CaptureGray(width, height, Level.Mesh, FigureLines())
        Catch ex As Exception
            Return New Byte(width * height - 1) {}
        End Try
    End Function

    ' /********************************************************************************/
    '  渲染辅助
    ' /********************************************************************************/

    ''' <summary>火柴人的渲染线段（骨架 + 头部线框 + 关节标记）。</summary>
    Public Function FigureLines() As List(Of LevelLine)
        Return FigureVisual.BuildFigureLines(Skeleton)
    End Function

    ''' <summary>把头部相机对齐到火柴人的头部。</summary>
    Public Sub SyncHeadCamera()
        Dim headPos As Vec3 = Skeleton.HeadPosition
        Dim fwd As Vec3 = Skeleton.HeadForward
        Dim up As Vec3 = Skeleton.HeadUp

        If fwd.LengthSquared < 1.0E-6 Then
            fwd = Skeleton.BodyForward
        End If

        ' 略微前移，避免眼点落在头部线框球内部导致自身几何被近平面裁掉
        Head.Camera.Eye = headPos + fwd.Normalize() * 0.06
        Head.Camera.Forward = fwd
        Head.Camera.Up = (up + New Vec3(0, 1, 0) * HeadUpBlend).Normalize()
        Call Head.Camera.Prepare()
    End Sub

    ''' <summary>是否已经跌倒。</summary>
    Public ReadOnly Property IsFallen As Boolean
        Get
            Return Skeleton.IsFallen
        End Get
    End Property

    ''' <summary>火柴人当前的世界坐标（骨盆）。</summary>
    Public ReadOnly Property Position As Vec3
        Get
            Return Skeleton.PelvisPosition
        End Get
    End Property

    ''' <summary>当前动作。</summary>
    Public ReadOnly Property CurrentAction As ActionPreset
        Get
            Return Director.Current
        End Get
    End Property
End Class
