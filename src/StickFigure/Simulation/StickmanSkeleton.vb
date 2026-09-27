Imports System.Math
Imports Microsoft.VisualBasic.Imaging.Physics
Imports Microsoft.VisualBasic.Imaging.Physics.Collision3D
Imports Microsoft.VisualBasic.Imaging.Physics.Joints3D
Imports Microsoft.VisualBasic.Imaging.Physics.Math3D
Imports std = System.Math

''' <summary>火柴人的骨骼部位。</summary>
Public Enum BoneIndex
    Pelvis
    Chest
    Neck
    Head
    UpperArmL
    LowerArmL
    UpperArmR
    LowerArmR
    ThighL
    ShinL
    FootL
    ThighR
    ShinR
    FootR
End Enum

''' <summary>火柴人的关节（每个关节同时挂一个球窝约束与一个角度马达）。</summary>
Public Enum JointIndex
    Spine
    NeckChest
    HeadNeck
    ShoulderL
    ElbowL
    ShoulderR
    ElbowR
    HipL
    KneeL
    AnkleL
    HipR
    KneeR
    AnkleR
End Enum

''' <summary>
''' 3D 火柴人骨架：15 个刚体 + 13 个球窝关节 + 13 个角度马达。
''' </summary>
''' <remarks>
''' 骨架在"人物自身坐标系"中定义（+Z 前、+Y 上、+X 右），构建时整体绕 Y 旋转
''' <paramref name="heading"/> 并平移到出生点。
'''
''' 姿态驱动采用"主动布娃娃"（active ragdoll）：球窝关节保证骨骼不脱臼，
''' <see cref="AngularMotor3D"/> 用 PD 控制把各关节拉向目标相对姿态，
''' 重力与碰撞完全由 <see cref="PhysicsWorld3D"/> 负责，因此角色会真实失稳跌倒。
''' </remarks>
Public Class StickmanSkeleton

    ' /********************************************************************************/
    '  骨骼与关节的静态定义
    ' /********************************************************************************/

    Private Class BoneDef
        Public Index As BoneIndex
        Public Proximal As String
        Public Distal As String
        Public Radius As Double
        Public Mass As Double
        Public PointsDown As Boolean
        Public IsSphere As Boolean
        Public Friction As Double
        ''' <summary>质心到端点的距离（渲染时求骨骼线段用）。</summary>
        Public HalfLength As Double
    End Class

    Private Class JointDef
        Public Index As JointIndex
        Public Parent As BoneIndex
        Public Child As BoneIndex
        Public Anchor As String
        Public Stiffness As Double
        Public Damping As Double
        Public MaxTorque As Double
    End Class

    Private Shared ReadOnly Property BoneTable As BoneDef()
        Get
            Return {
                New BoneDef With {.Index = BoneIndex.Pelvis, .Proximal = "hipCenter", .Distal = "waist", .Radius = 0.11, .Mass = 6.0, .PointsDown = False, .Friction = 0.35},
                New BoneDef With {.Index = BoneIndex.Chest, .Proximal = "waist", .Distal = "chestTop", .Radius = 0.13, .Mass = 9.0, .PointsDown = False, .Friction = 0.35},
                New BoneDef With {.Index = BoneIndex.Neck, .Proximal = "neckBase", .Distal = "neckTop", .Radius = 0.045, .Mass = 0.5, .PointsDown = False, .Friction = 0.35},
                New BoneDef With {.Index = BoneIndex.Head, .Proximal = "headBottom", .Distal = "headTop", .Radius = 0.115, .Mass = 3.0, .PointsDown = False, .IsSphere = True, .Friction = 0.35},
                New BoneDef With {.Index = BoneIndex.UpperArmL, .Proximal = "shoulderL", .Distal = "elbowL", .Radius = 0.055, .Mass = 1.0, .PointsDown = True, .Friction = 0.35},
                New BoneDef With {.Index = BoneIndex.LowerArmL, .Proximal = "elbowL", .Distal = "handL", .Radius = 0.048, .Mass = 0.7, .PointsDown = True, .Friction = 0.35},
                New BoneDef With {.Index = BoneIndex.UpperArmR, .Proximal = "shoulderR", .Distal = "elbowR", .Radius = 0.055, .Mass = 1.0, .PointsDown = True, .Friction = 0.35},
                New BoneDef With {.Index = BoneIndex.LowerArmR, .Proximal = "elbowR", .Distal = "handR", .Radius = 0.048, .Mass = 0.7, .PointsDown = True, .Friction = 0.35},
                New BoneDef With {.Index = BoneIndex.ThighL, .Proximal = "hipL", .Distal = "kneeL", .Radius = 0.078, .Mass = 2.6, .PointsDown = True, .Friction = 0.4},
                New BoneDef With {.Index = BoneIndex.ShinL, .Proximal = "kneeL", .Distal = "ankleL", .Radius = 0.062, .Mass = 1.8, .PointsDown = True, .Friction = 0.4},
                New BoneDef With {.Index = BoneIndex.FootL, .Proximal = "heelL", .Distal = "toeL", .Radius = 0.05, .Mass = 0.6, .PointsDown = True, .Friction = 0.95},
                New BoneDef With {.Index = BoneIndex.ThighR, .Proximal = "hipR", .Distal = "kneeR", .Radius = 0.078, .Mass = 2.6, .PointsDown = True, .Friction = 0.4},
                New BoneDef With {.Index = BoneIndex.ShinR, .Proximal = "kneeR", .Distal = "ankleR", .Radius = 0.062, .Mass = 1.8, .PointsDown = True, .Friction = 0.4},
                New BoneDef With {.Index = BoneIndex.FootR, .Proximal = "heelR", .Distal = "toeR", .Radius = 0.05, .Mass = 0.6, .PointsDown = True, .Friction = 0.95}
            }
        End Get
    End Property

    ''' <summary>
    ''' 关节表。
    ''' </summary>
    ''' <remarks>
    ''' 马达是显式积分的 PD 控制器，其自然频率 <c>ω = sqrt(Kp/I)</c> 必须远小于
    ''' <c>1/dt</c>（dt 为子步长 1/300 s）。各刚体的转动惯量相差两个数量级
    ''' （脚掌 1.4e-3 而躯干 1.5e-1 kg·m²），因此刚度一律按惯量标定：
    ''' <list type="bullet">
    ''' <item><description><c>Kp = ω²·I</c>，统一取 <c>ω = 35 rad/s</c>（<c>ω²≈1225</c>）→ 响应约 0.09 s，足以跟上 1~2 Hz 的步态；</description></item>
    ''' <item><description><c>Kd = 2·ω·I</c>，阻尼比 ≈ 1，无超调；</description></item>
    ''' </item></list>
    ''' 这样单帧角速度峰值被限制在 <c>0.7·ω ≈ 25 rad/s</c> 以内，
    ''' 每个子步的 Δω 约 1.8 rad/s，显式积分非常稳定。
    ''' </remarks>
    Private Shared ReadOnly Property JointTable As JointDef()
        Get
            Return {
                New JointDef With {.Index = JointIndex.Spine, .Parent = BoneIndex.Pelvis, .Child = BoneIndex.Chest, .Anchor = "waist", .Stiffness = 440, .Damping = 16.0, .MaxTorque = 320},
                New JointDef With {.Index = JointIndex.NeckChest, .Parent = BoneIndex.Chest, .Child = BoneIndex.Neck, .Anchor = "neckBase", .Stiffness = 1.9, .Damping = 0.075, .MaxTorque = 6},
                New JointDef With {.Index = JointIndex.HeadNeck, .Parent = BoneIndex.Neck, .Child = BoneIndex.Head, .Anchor = "neckTop", .Stiffness = 48, .Damping = 1.7, .MaxTorque = 40},
                New JointDef With {.Index = JointIndex.ShoulderL, .Parent = BoneIndex.Chest, .Child = BoneIndex.UpperArmL, .Anchor = "shoulderL", .Stiffness = 19, .Damping = 0.7, .MaxTorque = 40},
                New JointDef With {.Index = JointIndex.ElbowL, .Parent = BoneIndex.UpperArmL, .Child = BoneIndex.LowerArmL, .Anchor = "elbowL", .Stiffness = 11, .Damping = 0.4, .MaxTorque = 25},
                New JointDef With {.Index = JointIndex.ShoulderR, .Parent = BoneIndex.Chest, .Child = BoneIndex.UpperArmR, .Anchor = "shoulderR", .Stiffness = 19, .Damping = 0.7, .MaxTorque = 40},
                New JointDef With {.Index = JointIndex.ElbowR, .Parent = BoneIndex.UpperArmR, .Child = BoneIndex.LowerArmR, .Anchor = "elbowR", .Stiffness = 11, .Damping = 0.4, .MaxTorque = 25},
                New JointDef With {.Index = JointIndex.HipL, .Parent = BoneIndex.Pelvis, .Child = BoneIndex.ThighL, .Anchor = "hipL", .Stiffness = 116, .Damping = 4.2, .MaxTorque = 220},
                New JointDef With {.Index = JointIndex.KneeL, .Parent = BoneIndex.ThighL, .Child = BoneIndex.ShinL, .Anchor = "kneeL", .Stiffness = 81, .Damping = 2.9, .MaxTorque = 160},
                New JointDef With {.Index = JointIndex.AnkleL, .Parent = BoneIndex.ShinL, .Child = BoneIndex.FootL, .Anchor = "ankleL", .Stiffness = 4.3, .Damping = 0.16, .MaxTorque = 20},
                New JointDef With {.Index = JointIndex.HipR, .Parent = BoneIndex.Pelvis, .Child = BoneIndex.ThighR, .Anchor = "hipR", .Stiffness = 116, .Damping = 4.2, .MaxTorque = 220},
                New JointDef With {.Index = JointIndex.KneeR, .Parent = BoneIndex.ThighR, .Child = BoneIndex.ShinR, .Anchor = "kneeR", .Stiffness = 81, .Damping = 2.9, .MaxTorque = 160},
                New JointDef With {.Index = JointIndex.AnkleR, .Parent = BoneIndex.ShinR, .Child = BoneIndex.FootR, .Anchor = "ankleR", .Stiffness = 4.3, .Damping = 0.16, .MaxTorque = 20}
            }
        End Get
    End Property

    ''' <summary>
    ''' 静止站姿下的关节点坐标（人物自身坐标系：+Z 前、+Y 上、+X 右）。
    ''' </summary>
    ''' <remarks>
    ''' 脚掌的踝关节略高于脚底面，脚掌本体从脚跟一直延伸到脚尖：
    ''' 0.29 m 的前后支撑长度是站立平衡的必要条件，支撑面太窄会直接倾倒。
    ''' </remarks>
    Private Shared Function RestJointTable() As Dictionary(Of String, Vec3)
        Return New Dictionary(Of String, Vec3) From {
            {"hipCenter", New Vec3(0, 0.92, 0)},
            {"waist", New Vec3(0, 1.12, 0)},
            {"chestTop", New Vec3(0, 1.50, 0)},
            {"neckBase", New Vec3(0, 1.50, 0)},
            {"neckTop", New Vec3(0, 1.595, 0)},
            {"headBottom", New Vec3(0, 1.595, 0)},
            {"headTop", New Vec3(0, 1.825, 0)},
            {"shoulderL", New Vec3(-0.17, 1.44, 0)},
            {"shoulderR", New Vec3(0.17, 1.44, 0)},
            {"elbowL", New Vec3(-0.19, 1.18, 0)},
            {"elbowR", New Vec3(0.19, 1.18, 0)},
            {"handL", New Vec3(-0.20, 0.94, 0)},
            {"handR", New Vec3(0.20, 0.94, 0)},
            {"hipL", New Vec3(-0.09, 0.90, 0)},
            {"hipR", New Vec3(0.09, 0.90, 0)},
            {"kneeL", New Vec3(-0.10, 0.50, 0)},
            {"kneeR", New Vec3(0.10, 0.50, 0)},
            {"ankleL", New Vec3(-0.10, 0.10, 0.02)},
            {"ankleR", New Vec3(0.10, 0.10, 0.02)},
            {"heelL", New Vec3(-0.10, 0.05, -0.10)},
            {"heelR", New Vec3(0.10, 0.05, -0.10)},
            {"toeL", New Vec3(-0.10, 0.05, 0.19)},
            {"toeR", New Vec3(0.10, 0.05, 0.19)}
        }
    End Function

    ' /********************************************************************************/
    '  实例状态
    ' /********************************************************************************/

    ''' <summary>按部位索引的刚体。</summary>
    Public ReadOnly Property Bodies As New Dictionary(Of BoneIndex, RigidBody3D)()

    ''' <summary>按关节索引的球窝约束。</summary>
    Public ReadOnly Property Joints As New Dictionary(Of JointIndex, BallJoint3D)()

    ''' <summary>按关节索引的角度马达。</summary>
    Public ReadOnly Property Motors As New Dictionary(Of JointIndex, AngularMotor3D)()

    ''' <summary>各关节的静止（站立）目标相对姿态。</summary>
    Public ReadOnly Property RestTargets As New Dictionary(Of JointIndex, Quaternion)()

    Private ReadOnly boneDefs As New Dictionary(Of BoneIndex, BoneDef)()

    ''' <summary>马达刚度的全局缩放（UI 可实时调整）。</summary>
    Public Property MotorGainScale As Double = 1.0

    ''' <summary>出生位置。</summary>
    Public Property SpawnPosition As Vec3 = New Vec3(0, 0, 0)
    ''' <summary>出生朝向。</summary>
    Public Property SpawnHeading As Double = 0.0

    ''' <summary>全部刚体的线性列表（便于批量重置 / 施力）。</summary>
    Public ReadOnly Property AllBodies As List(Of RigidBody3D)
        Get
            Return Bodies.Values.ToList()
        End Get
    End Property

    ''' <summary>总体重。</summary>
    Public ReadOnly Property TotalMass As Double
        Get
            Return Bodies.Values.Sum(Function(b) b.Mass)
        End Get
    End Property

    ' /********************************************************************************/
    '  构建 / 复位
    ' /********************************************************************************/

    ''' <summary>
    ''' 在 <paramref name="world"/> 中创建骨架。
    ''' </summary>
    Public Sub Build(world As PhysicsWorld3D, position As Vec3, heading As Double)
        SpawnPosition = position
        SpawnHeading = heading

        Dim joints As Dictionary(Of String, Vec3) = WorldRestJoints(position, heading)

        For Each def As BoneDef In BoneTable
            Call CreateBone(world, def, joints)
        Next

        For Each def As JointDef In JointTable
            Call CreateJoint(world, def, joints)
        Next
    End Sub

    ''' <summary>把所有刚体恢复到静止站姿并清零速度。</summary>
    Public Sub Reset(Optional position As Vec3? = Nothing, Optional heading As Double = Double.NaN)
        If position.HasValue Then SpawnPosition = position.Value
        If Not Double.IsNaN(heading) Then SpawnHeading = heading

        Dim joints As Dictionary(Of String, Vec3) = WorldRestJoints(SpawnPosition, SpawnHeading)

        For Each def As BoneDef In BoneTable
            Dim proximal As Vec3 = joints(def.Proximal)
            Dim distal As Vec3 = joints(def.Distal)
            Dim body As RigidBody3D = Bodies(def.Index)
            Dim dir As Vector3 = (distal - proximal).ToPhysics()
            Dim length As Double = dir.Magnitude

            body.Position = ((proximal + distal) * 0.5).ToPhysics()
            body.Orientation = BoneOrientation(Vector3Math.Normalize(dir), def.PointsDown)
            body.Velocity = New Vector3(0, 0, 0)
            body.AngularVelocity = New Vector3(0, 0, 0)
            body.ClearForces()
        Next

        For Each kv In Motors
            kv.Value.TargetRelative = RestTargets(kv.Key)
        Next
    End Sub

    Private Function CreateBone(world As PhysicsWorld3D, def As BoneDef, jointTable As Dictionary(Of String, Vec3)) As RigidBody3D
        Dim proximal As Vec3 = jointTable(def.Proximal)
        Dim distal As Vec3 = jointTable(def.Distal)
        Dim delta As Vec3 = distal - proximal
        Dim length As Double = delta.Length
        Dim center As Vec3 = (proximal + distal) * 0.5
        Dim orientation As Quaternion = BoneOrientation(delta.Normalize().ToPhysics(), def.PointsDown)
        Dim material As New PhysicsMaterial(def.Friction, 0.0)
        Dim body As RigidBody3D

        If def.IsSphere Then
            body = PhysicsWorld3D.Sphere(def.Radius, def.Mass, material)
            def.HalfLength = def.Radius
        Else
            body = PhysicsWorld3D.Capsule(def.Radius, std.Max(length, 0.01), def.Mass, material)
            def.HalfLength = length * 0.5
        End If

        body.Position = center.ToPhysics()
        body.Orientation = orientation
        body.Label = def.Index.ToString()
        body.LinearDamping = 0.02
        body.AngularDamping = 0.15
        body.MaxSpeed = 30.0

        ' 火柴人只与关卡几何(group 1)碰撞，自身各部位互不碰撞
        body.CollisionGroup = 2
        body.CollisionMask = 1

        world.Add(body)
        Bodies(def.Index) = body
        boneDefs(def.Index) = def

        Return body
    End Function

    Private Sub CreateJoint(world As PhysicsWorld3D, def As JointDef, jointTable As Dictionary(Of String, Vec3))
        Dim parent As RigidBody3D = Bodies(def.Parent)
        Dim child As RigidBody3D = Bodies(def.Child)
        Dim anchorWorld As Vector3 = jointTable(def.Anchor).ToPhysics()
        Dim joint As New BallJoint3D(parent, child,
                                     parent.ToLocal(anchorWorld),
                                     child.ToLocal(anchorWorld))

        joint.limitAxisA = If(boneDefs(def.Child).PointsDown, New Vector3(0, -1, 0), New Vector3(0, 1, 0))
        joint.limitAxisB = New Vector3(0, 1, 0)

        world.Add(joint)
        Joints(def.Index) = joint

        Dim motor As New AngularMotor3D(parent, child,
                                        Quaternion.Compose(parent.Orientation.Conjugate(), child.Orientation)) With {
            .Stiffness = def.Stiffness * MotorGainScale,
            .Damping = def.Damping * MotorGainScale,
            .MaxTorque = def.MaxTorque,
            .Label = def.Index.ToString()
        }

        world.Add(motor)
        Motors(def.Index) = motor
        RestTargets(def.Index) = motor.TargetRelative
    End Sub

    Private Shared Function WorldRestJoints(position As Vec3, heading As Double) As Dictionary(Of String, Vec3)
        Dim q As Quaternion = Quaternion.FromAxisAngle(New Vector3(0, 1, 0), heading)
        Dim local As Dictionary(Of String, Vec3) = RestJointTable()
        Dim result As New Dictionary(Of String, Vec3)()

        For Each kv In local
            Dim rotated As Vec3 = Vec3.FromPhysics(q.Rotate(kv.Value.ToPhysics()))
            result(kv.Key) = rotated + position
        Next

        Return result
    End Function

    ''' <summary>
    ''' 由骨骼方向求刚体姿态：保证"局部 X 轴 = 左右轴"，这样所有关节绕局部 X 的旋转
    ''' 都是解剖学上的屈伸，符号在整个骨架上保持一致。
    ''' </summary>
    Private Shared Function BoneOrientation(dir As Vector3, pointsDown As Boolean) As Quaternion
        Dim up As New Vector3(0, 1, 0)

        If Not pointsDown Then
            Return RotationToDirection(up, dir)
        End If

        ' 先绕 X 轴翻转 180°（局部 +Y 指向下方，局部 X 仍为左右轴），再补一个小角度修正
        Dim flip As Quaternion = Quaternion.FromAxisAngle(New Vector3(1, 0, 0), std.PI)
        Dim dirLocal As Vector3 = flip.Unrotate(dir)

        Return Quaternion.Compose(flip, RotationToDirection(up, dirLocal))
    End Function

    ''' <summary>把 <paramref name="from"/> 旋转到 <paramref name="to"/> 的最小旋转四元数。</summary>
    Private Shared Function RotationToDirection(from As Vector3, [to] As Vector3) As Quaternion
        Dim a As Vector3 = Vector3Math.Normalize(from)
        Dim b As Vector3 = Vector3Math.Normalize([to])
        Dim dot As Double = std.Min(1.0, std.Max(-1.0, Vector3Math.Dot(a, b)))

        If dot > 0.999999 Then
            Return Quaternion.Identity
        End If
        If dot < -0.999999 Then
            Return Quaternion.FromAxisAngle(New Vector3(1, 0, 0), std.PI)
        End If

        Dim axis As Vector3 = Vector3Math.Normalize(Vector3Math.Cross(a, b))

        Return Quaternion.FromAxisAngle(axis, std.Acos(dot))
    End Function

    ' /********************************************************************************/
    '  姿态驱动
    ' /********************************************************************************/

    ''' <summary>把目标姿态写入各关节马达。</summary>
    Public Sub ApplyPose(pose As StickmanPose)
        ' 下肢：pitch 取负号是因为下肢的局部 +Y 指向下方（见 BoneOrientation）
        Call SetLimb(JointIndex.HipL, RestTargets(JointIndex.HipL), -pose.HipPitchL, pose.HipRollL, 0)
        Call SetLimb(JointIndex.HipR, RestTargets(JointIndex.HipR), -pose.HipPitchR, pose.HipRollR, 0)
        Call SetLimb(JointIndex.KneeL, RestTargets(JointIndex.KneeL), pose.KneeL, 0, 0)
        Call SetLimb(JointIndex.KneeR, RestTargets(JointIndex.KneeR), pose.KneeR, 0, 0)
        Call SetLimb(JointIndex.AnkleL, RestTargets(JointIndex.AnkleL), pose.AnkleL, 0, 0)
        Call SetLimb(JointIndex.AnkleR, RestTargets(JointIndex.AnkleR), pose.AnkleR, 0, 0)

        ' 上肢：与下肢同样的符号约定
        Call SetLimb(JointIndex.ShoulderL, RestTargets(JointIndex.ShoulderL), -pose.ShoulderPitchL, pose.ShoulderRollL, 0)
        Call SetLimb(JointIndex.ShoulderR, RestTargets(JointIndex.ShoulderR), -pose.ShoulderPitchR, pose.ShoulderRollR, 0)
        Call SetLimb(JointIndex.ElbowL, RestTargets(JointIndex.ElbowL), pose.ElbowL, 0, 0)
        Call SetLimb(JointIndex.ElbowR, RestTargets(JointIndex.ElbowR), pose.ElbowR, 0, 0)

        ' 躯干：局部 +Y 朝上，pitch 正 = 前倾
        Call SetLimb(JointIndex.Spine, RestTargets(JointIndex.Spine), pose.SpinePitch, pose.SpineRoll, pose.SpineYaw)
        Call SetLimb(JointIndex.NeckChest, RestTargets(JointIndex.NeckChest), pose.NeckPitch, 0, pose.NeckYaw)
    End Sub

    ''' <summary>在静止姿态基础上叠加绕局部 X/Z/Y 的增量旋转。</summary>
    Private Sub SetLimb(index As JointIndex, rest As Quaternion,
                        pitchX As Double, rollZ As Double, yawY As Double)
        Dim q As Quaternion = rest

        If pitchX <> 0.0 Then
            q = Quaternion.Compose(q, Quaternion.FromAxisAngle(New Vector3(1, 0, 0), pitchX))
        End If
        If rollZ <> 0.0 Then
            q = Quaternion.Compose(q, Quaternion.FromAxisAngle(New Vector3(0, 0, 1), rollZ))
        End If
        If yawY <> 0.0 Then
            q = Quaternion.Compose(q, Quaternion.FromAxisAngle(New Vector3(0, 1, 0), yawY))
        End If

        Motors(index).TargetRelative = q
    End Sub

    ''' <summary>实时调整马达刚度（UI 调参用）。</summary>
    Public Sub ApplyMotorGain(scale As Double)
        MotorGainScale = scale

        For Each def As JointDef In JointTable
            Motors(def.Index).Stiffness = def.Stiffness * scale
            Motors(def.Index).Damping = def.Damping * scale
        Next
    End Sub

    ' /********************************************************************************/
    '  查询
    ' /********************************************************************************/

    ''' <summary>骨骼两端的世界坐标（渲染骨架线段用）。</summary>
    Public Function BoneSegment(index As BoneIndex) As (A As Vec3, B As Vec3)
        Dim body As RigidBody3D = Bodies(index)
        Dim half As Double = boneDefs(index).HalfLength
        Dim a As Vec3 = Vec3.FromPhysics(body.Position + body.Orientation.Rotate(New Vector3(0, -half, 0)))
        Dim b As Vec3 = Vec3.FromPhysics(body.Position + body.Orientation.Rotate(New Vector3(0, half, 0)))

        Return (a, b)
    End Function

    ''' <summary>关节锚点的世界坐标。</summary>
    Public Function JointPosition(index As JointIndex) As Vec3
        Return Vec3.FromPhysics(Joints(index).WorldAnchorA)
    End Function

    ''' <summary>头部球心。</summary>
    Public ReadOnly Property HeadPosition As Vec3
        Get
            Return Vec3.FromPhysics(Bodies(BoneIndex.Head).Position)
        End Get
    End Property

    ''' <summary>头部朝向（前向量）。</summary>
    Public ReadOnly Property HeadForward As Vec3
        Get
            Return Vec3.FromPhysics(Bodies(BoneIndex.Head).Forward)
        End Get
    End Property

    ''' <summary>头部上方向。</summary>
    Public ReadOnly Property HeadUp As Vec3
        Get
            Return Vec3.FromPhysics(Bodies(BoneIndex.Head).Up)
        End Get
    End Property

    ''' <summary>骨盆质心。</summary>
    Public ReadOnly Property PelvisPosition As Vec3
        Get
            Return Vec3.FromPhysics(Bodies(BoneIndex.Pelvis).Position)
        End Get
    End Property

    ''' <summary>胸部质心。</summary>
    Public ReadOnly Property ChestPosition As Vec3
        Get
            Return Vec3.FromPhysics(Bodies(BoneIndex.Chest).Position)
        End Get
    End Property

    ''' <summary>头部到骨盆的连线方向（躯干轴向）。</summary>
    Public ReadOnly Property TorsoUp As Vec3
        Get
            Return (ChestPosition - PelvisPosition).Normalize()
        End Get
    End Property

    ''' <summary>躯干前向量（由胸部的局部 +Z 投影到水平面得到）。</summary>
    Public ReadOnly Property BodyForward As Vec3
        Get
            Dim f As Vec3 = Vec3.FromPhysics(Bodies(BoneIndex.Chest).Forward)
            Dim flat As New Vec3(f.X, 0, f.Z)

            If flat.LengthSquared < 1.0E-8 Then
                Return Level.HeadingToForward(SpawnHeading)
            End If

            Return flat.Normalize()
        End Get
    End Property

    ''' <summary>整体质心的水平位置。</summary>
    Public ReadOnly Property CenterOfMass As Vec3
        Get
            Dim sum As New Vec3(0, 0, 0)
            Dim total As Double = 0.0

            For Each b As RigidBody3D In Bodies.Values
                sum = sum + Vec3.FromPhysics(b.Position) * b.Mass
                total += b.Mass
            Next

            Return sum / std.Max(total, 1.0E-9)
        End Get
    End Property

    ''' <summary>整体质心的速度。</summary>
    Public ReadOnly Property CenterOfMassVelocity As Vec3
        Get
            Dim sum As New Vec3(0, 0, 0)
            Dim total As Double = 0.0

            For Each b As RigidBody3D In Bodies.Values
                sum = sum + Vec3.FromPhysics(b.Velocity) * b.Mass
                total += b.Mass
            Next

            Return sum / std.Max(total, 1.0E-9)
        End Get
    End Property

    ''' <summary>双脚是否已经踩到东西（由物理世界的接触标记给出）。</summary>
    Public ReadOnly Property FootContact As (Left As Boolean, Right As Boolean)
        Get
            Return (Bodies(BoneIndex.FootL).HadContact, Bodies(BoneIndex.FootR).HadContact)
        End Get
    End Property

    ''' <summary>是否已经跌倒（头部过低或躯干过分倾斜）。</summary>
    Public ReadOnly Property IsFallen As Boolean
        Get
            Dim head As Vec3 = HeadPosition
            Dim pelvis As Vec3 = PelvisPosition

            If head.Y < 0.55 Then
                Return True
            End If
            If TorsoUp.Y < 0.35 Then
                Return True
            End If

            Return False
        End Get
    End Property

    ''' <summary>躯干相对竖直方向的倾角（弧度）。</summary>
    Public ReadOnly Property TiltAngle As Double
        Get
            Return std.Acos(std.Min(1.0, std.Max(-1.0, TorsoUp.Y)))
        End Get
    End Property

    ''' <summary>给全身一个向上的起跳速度（m/s）。</summary>
    Public Sub ApplyJump(speed As Double)
        If speed <= 0 Then Return

        For Each b As RigidBody3D In Bodies.Values
            Call b.ApplyImpulse(New Vector3(0, speed * b.Mass, 0), New Vector3(0, 0, 0))
        Next
    End Sub
End Class
