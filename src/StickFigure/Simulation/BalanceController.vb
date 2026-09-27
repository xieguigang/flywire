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
Public Class BalanceController

    ' ---------- 躯干直立 ----------
    ''' <summary>直立比例增益（rad/s² per rad）。</summary>
    Public Property UprightKp As Double = 130.0
    ''' <summary>直立微分增益。</summary>
    Public Property UprightKd As Double = 22.0
    ''' <summary>直立角加速度上限。</summary>
    Public Property UprightMaxAlpha As Double = 70.0

    ' ---------- 朝向 ----------
    ''' <summary>朝向比例增益。</summary>
    Public Property HeadingKp As Double = 45.0
    ''' <summary>朝向微分增益。</summary>
    Public Property HeadingKd As Double = 9.0
    ''' <summary>朝向角加速度上限。</summary>
    Public Property HeadingMaxAlpha As Double = 40.0

    ' ---------- 骨盆高度 ----------
    ''' <summary>高度比例增益（m/s² per m）。</summary>
    Public Property HeightKp As Double = 240.0
    ''' <summary>高度微分增益。</summary>
    Public Property HeightKd As Double = 26.0
    ''' <summary>向上补偿加速度上限（约 1.5g）。</summary>
    Public Property HeightMaxAccel As Double = 15.0

    ' ---------- 水平驱动 ----------
    ''' <summary>水平驱动比例增益（1/s）。</summary>
    Public Property DriveKp As Double = 7.0
    ''' <summary>水平驱动加速度上限。</summary>
    Public Property DriveMaxAccel As Double = 9.0

    ' ---------- 质心支撑 ----------
    ''' <summary>质心回中比例增益。</summary>
    Public Property ComKp As Double = 5.0
    ''' <summary>质心回中加速度上限。</summary>
    Public Property ComMaxAccel As Double = 5.0
    ''' <summary>是否启用质心回中反射。行走不稳时打开，追求自然步态时可关闭。</summary>
    Public Property EnableComBalance As Boolean = True

    ''' <summary>总开关。</summary>
    Public Property Enabled As Boolean = True

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

        Dim torque As Vector3 = chest.InertiaWorld().MultiplyLeft(alpha)

        Call chest.ApplyTorque(torque)
        Call pelvis.ApplyTorque(torque * -0.6)
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
        Call pelvis.ApplyTorque(torque * -0.6)
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
        Dim accelXZ As Vec3 = (vWant - vHoriz) * DriveKp

        accelXZ = ClampVec3(accelXZ, DriveMaxAccel)

        ' ---- 质心回中 ----
        Dim com As Vec3 = skel.CenterOfMass
        Dim support As Vec3 = (Vec3.FromPhysics(skel.Bodies(BoneIndex.FootL).Position) +
                               Vec3.FromPhysics(skel.Bodies(BoneIndex.FootR).Position)) * 0.5
        Dim comErr As New Vec3(support.X - com.X, 0, support.Z - com.Z)

        If EnableComBalance Then
            accelXZ = accelXZ + ClampVec3(comErr * ComKp, ComMaxAccel)
        End If

        Dim force As New Vector3(accelXZ.X, accelY, accelXZ.Z) * total

        ' 把躯干外力分摊到骨盆与胸部，避免单点受力过大导致关节抖动
        Call pelvis.ApplyForce(force * 0.6)
        Call chest.ApplyForce(force * 0.4)
    End Sub

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
