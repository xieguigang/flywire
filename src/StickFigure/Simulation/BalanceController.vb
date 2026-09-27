Imports System.Math
Imports Microsoft.VisualBasic.Imaging.Physics
Imports Microsoft.VisualBasic.Imaging.Physics.Math3D
Imports std = System.Math

''' <summary>
''' 平衡控制器：在关节马达之外提供三路"反射"，让火柴人能够克服重力保持站立。
''' </summary>
''' <remarks>
''' <list type="number">
''' <item><description><strong>躯干直立</strong>：绕最短轴把胸部 up 向量拉回世界竖直方向；</description></item>
''' <item><description><strong>朝向保持</strong>：绕竖直轴把躯干前向转到目标朝向；</description></item>
''' <item><description><strong>骨盆高度</strong>：只在骨盆低于目标时向上补力，等效一对"虚拟腿弹簧"；</description></item>
''' <item><description><strong>水平驱动</strong>：把躯干水平速度拉向步态给定的期望速度（行走的动力来源）；</description></item>
''' <item><description><strong>质心支撑</strong>：把整体质心拉回双脚支撑面中心。</description></item>
''' </list>
''' 直立与朝向以"角加速度"形式给出，内部乘以世界系惯性张量换算成扭矩，
''' 因此增益与刚体质量 / 尺寸解耦，调参更直观。
''' </remarks>
Public Class BalanceController : Implements IStepHook

    ' ---------- 躯干直立 ----------
    ''' <summary>直立比例增益（rad/s² per rad）。</summary>
    Public Property UprightKp As Double = 1200.0
    ''' <summary>直立微分增益。</summary>
    Public Property UprightKd As Double = 60.0
    ''' <summary>直立角加速度上限：显式积分的稳定闸门，必须限制在 1/dt 量级以内。</summary>
    Public Property UprightMaxAlpha As Double = 400.0
    ''' <summary>
    ''' 骨盆直立扭矩的额外增益。
    ''' </summary>
    ''' <remarks>
    ''' 骨盆自身惯量小，按与胸部相同的角加速度只能得到 ~12 N·m，
    ''' 不足以扶住上半身；这里单独放大（对应 ~1200 rad/s²，
    ''' 每子步 Δω ≈ 3.3 rad/s，仍在显式积分的稳定范围内）。
    ''' </remarks>
    Public Property PelvisUprightGain As Double = 3.0
    ''' <summary>骨盆直立角加速度上限。</summary>
    Public Property PelvisUprightMaxAlpha As Double = 1200.0

    ' ---------- 朝向 ----------
    ''' <summary>
    ''' 朝向比例增益（rad/s² per rad）。
    ''' </summary>
    ''' <remarks>
    ''' 这里必须取得比直立反射保守得多：骨盆通过双腿踩在地上，
    ''' 绕竖直轴的有效惯量是骨盆自身惯量的十倍以上，若按
    ''' <see cref="RigidBody3D.InertiaWorld"/> 标定增益会产生严重过冲，
    ''' 表现为躯干原地来回甩（前进速度被抵消成 0）。因此增益压低、
    ''' 并且<strong>只</strong>作用在胸部，让下肢通过关节约束自然跟随。
    ''' </remarks>
    Public Property HeadingKp As Double = 50.0
    ''' <summary>朝向微分增益。</summary>
    Public Property HeadingKd As Double = 26.0
    ''' <summary>朝向角加速度上限。</summary>
    Public Property HeadingMaxAlpha As Double = 60.0

    ' ---------- 骨盆高度 ----------
    ''' <summary>高度比例增益（m/s² per m）。</summary>
    Public Property HeightKp As Double = 400.0
    ''' <summary>高度微分增益。</summary>
    Public Property HeightKd As Double = 38.0
    ''' <summary>向上补偿加速度上限（约 2g）。</summary>
    Public Property HeightMaxAccel As Double = 20.0

    ' ---------- 水平驱动 ----------
    ''' <summary>水平驱动比例增益（1/s）。</summary>
    Public Property DriveKp As Double = 6.0
    ''' <summary>水平驱动加速度上限：推得太猛会让角色前倾扑倒。</summary>
    Public Property DriveMaxAccel As Double = 8.0

    ' ---------- 质心支撑 ----------
    ''' <summary>
    ''' 质心回中比例增益（m/s² per m）。
    ''' 倒立摆模型下保持倾角 θ 需要 <c>g·tan θ</c> 的水平加速度，
    ''' COM 偏移约 <c>0.9·sin θ</c>，因此增益至少要 12 才能覆盖中等倾角。
    ''' </summary>
    Public Property ComKp As Double = 45.0
    ''' <summary>
    ''' 质心回中的<strong>纵向</strong>（沿行进方向）增益。
    ''' </summary>
    ''' <remarks>
    ''' 行走时质心本来就应该落在支撑面前方，若纵向也用横向那么大的增益，
    ''' 回中反射会把水平驱动完全抵消，角色只能原地踏步。
    ''' </remarks>
    Public Property ComKpLongitudinal As Double = 5.0
    ''' <summary>质心回中微分增益，抑制来回震荡。</summary>
    Public Property ComKd As Double = 9.0
    ''' <summary>
    ''' 质心回中加速度上限。上限受地面摩擦约束：
    ''' <c>F = m·a</c> 必须小于 <c>μ·m·g</c>，否则脚会打滑。
    ''' </summary>
    Public Property ComMaxAccel As Double = 8.0
    ''' <summary>是否启用质心回中反射。行走不稳时打开，追求自然步态时可关闭。</summary>
    Public Property EnableComBalance As Boolean = True

    ''' <summary>总开关。</summary>
    Public Property Enabled As Boolean = True

    ' /********************************************************************************/
    '  子步钩子
    ' /********************************************************************************/

    ''' <summary>被控制的骨架（由 <c>FigureEnvironment</c> 装配时注入）。</summary>
    Public Property Target As StickmanSkeleton

    ''' <summary>当前目标姿态（由 <c>FigureEnvironment</c> 注入，每帧更新）。</summary>
    Public Property PoseRef As StickmanPose

    ''' <summary>脚下的地面高度（每帧由关卡查询后写入）。</summary>
    Public Property GroundHeight As Double = 0.0

    ''' <summary>
    ''' 每个物理子步都会调用一次。
    ''' </summary>
    ''' <remarks>
    ''' 平衡反射必须逐子步施加：<c>PhysicsWorld3D</c> 在每个子步末尾清除力累加器，
    ''' 只在帧首施加一次的话，等效强度会被稀释成 <c>1/Substeps</c>，角色会直接瘫下去。
    ''' </remarks>
    Public Sub BeforeSubstep(dt As Double) Implements IStepHook.BeforeSubstep
        If Target Is Nothing OrElse PoseRef Is Nothing Then
            Return
        End If

        Call Apply(Target, PoseRef, GroundHeight, dt)
    End Sub

    ''' <summary>
    ''' 施加本帧的平衡力矩 / 力。应在 <c>PhysicsWorld3D.Step</c> 之前调用。
    ''' </summary>
    ''' <param name="groundHeight">火柴人脚下的地面高度（上台阶时会变化）。</param>
    Public Sub Apply(skel As StickmanSkeleton, pose As StickmanPose, groundHeight As Double, dt As Double)
        If Not Enabled OrElse dt <= 0 Then
            Return
        End If

        Dim chest As RigidBody3D = skel.Bodies(BoneIndex.Chest)
        Dim pelvis As RigidBody3D = skel.Bodies(BoneIndex.Pelvis)
        Dim worldUp As New Vector3(0, 1, 0)

        Call ApplyUpright(chest, pelvis, worldUp)
        Call ApplyHeading(skel, chest, pelvis, pose, worldUp)
        Call ApplyTrunkForce(skel, pose, groundHeight)
    End Sub

    ' /********************************************************************************/
    '  反射 1：躯干直立
    ' /********************************************************************************/

    Private Sub ApplyUpright(chest As RigidBody3D, pelvis As RigidBody3D, worldUp As Vector3)
        Dim up As Vector3 = chest.Up
        Dim dot As Double = std.Min(1.0, std.Max(-1.0, Vector3Math.Dot(up, worldUp)))
        Dim angle As Double = std.Acos(dot)
        Dim alpha As New Vector3(0, 0, 0)

        If angle > 1.0E-5 Then
            Dim axis As Vector3 = Vector3Math.Cross(up, worldUp)

            If Vector3Math.LengthSquared(axis) > 1.0E-12 Then
                axis = Vector3Math.Normalize(axis)
                alpha = alpha + axis * (angle * UprightKp)
            End If
        End If

        ' 只阻尼倾斜分量，避免与朝向控制器互相抵消
        Dim omega As Vector3 = chest.AngularVelocity
        Dim omegaTilt As Vector3 = omega - worldUp * Vector3Math.Dot(omega, worldUp)

        alpha = alpha - omegaTilt * UprightKd
        alpha = ClampLength(alpha, UprightMaxAlpha)

        ' 胸部与骨盆同时被"扶正"（而不是一个受力、另一个受反作用力）：
        ' 主动布娃娃里的平衡辅助本质上是外力，若只在胸部施加反作用力矩，
        ' 躯干会绕髋关节折起来而整个人继续倒。
        '
        ' 骨盆要单独加强：它是全身的"根"，自身惯量只有 0.038 kg·m²，
        ' 按同样的角加速度算出来的扭矩 (~12 N·m) 远不够抵消上半身前倾的
        ' 重力矩（~50 N·m），角色会整个向前折下去。这里用独立增益放大。
        Call chest.ApplyTorque(chest.InertiaWorld().MultiplyLeft(alpha))
        Call pelvis.ApplyTorque(pelvis.InertiaWorld().MultiplyLeft(ClampLength(alpha * PelvisUprightGain, PelvisUprightMaxAlpha)))
    End Sub

    ' /********************************************************************************/
    '  反射 2：朝向
    ' /********************************************************************************/

    Private Sub ApplyHeading(skel As StickmanSkeleton, chest As RigidBody3D, pelvis As RigidBody3D,
                             pose As StickmanPose, worldUp As Vector3)
        Dim fwd As Vec3 = skel.BodyForward
        Dim desired As Vec3 = Level.HeadingToForward(pose.TargetHeading)
        Dim dot As Double = std.Min(1.0, std.Max(-1.0, Vec3.Dot(fwd, desired)))
        Dim angle As Double = std.Acos(dot)

        If angle < 1.0E-4 Then
            ' 已经对准：仍然阻尼偏航角速度，避免原地打转
            Dim wy As Double = Vector3Math.Dot(chest.AngularVelocity, worldUp)
            Dim damp As Vector3 = worldUp * (-wy * HeadingKd)

            Call chest.ApplyTorque(chest.InertiaWorld().MultiplyLeft(ClampLength(damp, HeadingMaxAlpha)))
            Return
        End If

        ' 水平面内旋转轴：Cross(fwd, desired) 只有 Y 分量
        Dim axisY As Double = fwd.Z * desired.X - fwd.X * desired.Z
        Dim axis As Vector3 = worldUp * If(axisY >= 0, 1.0, -1.0)
        Dim wyNow As Double = Vector3Math.Dot(chest.AngularVelocity, worldUp)
        Dim alpha As Vector3 = axis * (angle * HeadingKp) - worldUp * (wyNow * HeadingKd)

        alpha = ClampLength(alpha, HeadingMaxAlpha)

        Dim torque As Vector3 = chest.InertiaWorld().MultiplyLeft(alpha)

        Call chest.ApplyTorque(torque)
    End Sub

    ' /********************************************************************************/
    '  反射 3/4/5：躯干外力（高度、驱动、质心）
    ' /********************************************************************************/

    Private Sub ApplyTrunkForce(skel As StickmanSkeleton, pose As StickmanPose, groundHeight As Double)
        Dim chest As RigidBody3D = skel.Bodies(BoneIndex.Chest)
        Dim pelvis As RigidBody3D = skel.Bodies(BoneIndex.Pelvis)
        Dim total As Double = std.Max(skel.TotalMass, 1.0)

        ' ---- 高度：只在低于目标时向上补力 ----
        Dim targetY As Double = groundHeight + pose.PelvisHeight
        Dim errY As Double = targetY - pelvis.Position.y
        Dim accelY As Double = errY * HeightKp - pelvis.Velocity.y * HeightKd

        accelY = std.Min(std.Max(accelY, 0.0), HeightMaxAccel)

        ' ---- 水平驱动 ----
        Dim vHoriz As New Vec3(pelvis.Velocity.x, 0, pelvis.Velocity.z)
        Dim vWant As Vec3 = Level.HeadingToForward(pose.TargetHeading) * pose.TargetSpeed
        Dim accelXZ As Vec3 = ClampVec3((vWant - vHoriz) * DriveKp, DriveMaxAccel)

        ' ---- 质心回中：把整体质心拉回双脚支撑面中心（倒立摆的"踩回来"反射）----
        Dim com As Vec3 = skel.CenterOfMass
        Dim support As Vec3 = WeightedSupportPoint(skel)
        Dim comVel As New Vec3(skel.CenterOfMassVelocity.X, 0, skel.CenterOfMassVelocity.Z)
        Dim comErr As New Vec3(support.X - com.X, 0, support.Z - com.Z)

        If EnableComBalance AndAlso pose.ComAssist > 0.0 Then
            Dim fwdDir As Vec3 = Level.HeadingToForward(pose.TargetHeading)
            Dim rightDir As Vec3 = Vec3.Cross(New Vec3(0, 1, 0), fwdDir).Normalize()
            Dim errLong As Double = Vec3.Dot(comErr, fwdDir)
            Dim errLat As Double = Vec3.Dot(comErr, rightDir)

            ' 阻尼项用"相对期望速度"的偏差，否则行走时它会把前进驱动一起抵消掉
            accelXZ = accelXZ + ClampVec3(
                fwdDir * (errLong * ComKpLongitudinal) +
                rightDir * (errLat * ComKp) -
                (comVel - vWant) * ComKd,
                ComMaxAccel) * pose.ComAssist
        End If

        Dim forceDir As New Vector3(accelXZ.X, accelY, accelXZ.Z)
        Dim force As Vector3 = forceDir * total

        ' 把躯干外力分摊到骨盆与胸部，避免单点受力过大导致关节抖动
        Call pelvis.ApplyForce(force * 0.6)
        Call chest.ApplyForce(force * 0.4)
    End Sub

    ''' <summary>
    ''' 按触地与否加权的支撑点：摆动腿几乎不承重，若把它的踝关节也算进支撑中心，
    ''' 支撑参考会被"拖"到身体后方，质心回中反射就会把角色往后推倒。
    ''' </summary>
    Private Shared Function WeightedSupportPoint(skel As StickmanSkeleton) As Vec3
        Dim ankleL As Vec3 = skel.JointPosition(JointIndex.AnkleL)
        Dim ankleR As Vec3 = skel.JointPosition(JointIndex.AnkleR)
        Dim wL As Double = If(skel.Bodies(BoneIndex.FootL).HadContact, 1.0, 0.12)
        Dim wR As Double = If(skel.Bodies(BoneIndex.FootR).HadContact, 1.0, 0.12)

        Return (ankleL * wL + ankleR * wR) / (wL + wR)
    End Function

    Private Shared Function ClampLength(v As Vector3, maxLength As Double) As Vector3
        Dim len As Double = Vector3Math.Length(v)

        If len <= maxLength OrElse len < 1.0E-12 Then
            Return v
        End If

        Return v * (maxLength / len)
    End Function

    Private Shared Function ClampVec3(v As Vec3, maxLength As Double) As Vec3
        Dim len As Double = v.Length

        If len <= maxLength OrElse len < 1.0E-12 Then
            Return v
        End If

        Return v * (maxLength / len)
    End Function
End Class
