Imports System.Drawing
Imports Microsoft.VisualBasic.Imaging.Physics
Imports Microsoft.VisualBasic.Imaging.Physics.Collision3D
Imports std = System.Math

''' <summary>
''' 一整套静态关卡：物理刚体 + 渲染几何 + 供脚本查询的关键位置。
''' </summary>
Public Class Level

    ''' <summary>渲染几何（主视角与头部视角共用）。</summary>
    Public ReadOnly Property Mesh As New LevelMesh()

    ''' <summary>注册到物理世界的静态刚体。</summary>
    Public ReadOnly Property Bodies As New List(Of RigidBody3D)()

    ''' <summary>地面高度（Y）。</summary>
    Public Property GroundY As Double = 0.0

    ''' <summary>火柴人的出发位置。</summary>
    Public Property StartPosition As Vec3 = New Vec3(0, 0, 0)

    ''' <summary>出发朝向（弧度，0 = +X 方向）。</summary>
    Public Property StartHeading As Double = 0.0

    ''' <summary>需要跨越的低矮障碍所在的 X 坐标。</summary>
    Public Property ObstacleX As Double = 8.0

    ''' <summary>障碍高度。</summary>
    Public Property ObstacleHeight As Double = 0.36

    ''' <summary>台阶起始 X 坐标。</summary>
    Public Property StairsStartX As Double = 14.0

    ''' <summary>台阶顶部平台高度。</summary>
    Public Property StairsTopY As Double = 0.88

    ''' <summary>
    ''' 由朝向角求单位前向量。
    ''' </summary>
    ''' <remarks>
    ''' 与骨架构建时绕 +Y 轴的旋转保持一致：heading=0 → +Z，
    ''' 绕 +Y 旋转 h 后 (0,0,1) → (sin h, 0, cos h)。
    ''' </remarks>
    Public Shared Function HeadingToForward(heading As Double) As Vec3
        Return New Vec3(Math.Sin(heading), 0, Math.Cos(heading))
    End Function

    ''' <summary>
    ''' 查询 <paramref name="x"/>, <paramref name="z"/> 处的地面高度。
    ''' 供平衡控制器在上台阶 / 站上平台时动态调整骨盆目标高度。
    ''' </summary>
    Public Function GroundHeightAt(x As Double, z As Double) As Double
        Dim height As Double = GroundY

        For Each body As RigidBody3D In Bodies
            If Not body.IsStatic Then
                Continue For
            End If
            If TypeOf body.Shape Is BoxCollider3D Then
                Dim box As BoxCollider3D = DirectCast(body.Shape, BoxCollider3D)
                Dim half As Vector3 = box.HalfExtents
                Dim p As Vector3 = body.Position

                If x >= p.x - half.x AndAlso x <= p.x + half.x AndAlso
                   z >= p.z - half.z AndAlso z <= p.z + half.z Then
                    height = Math.Max(height, p.y + half.y)
                End If
            End If
        Next

        Return height
    End Function
End Class

''' <summary>
''' 关卡构建器：生成"平地 → 低矮障碍 → 四级台阶 → 高台"的默认测试场景。
''' </summary>
Public Module LevelBuilder

    ''' <summary>障碍物的渲染颜色。</summary>
    Public ReadOnly Property ObstacleColor As Color = Color.FromArgb(&HFF, &HD1, &H66)
    ''' <summary>台阶的渲染颜色。</summary>
    Public ReadOnly Property StairColor As Color = Color.FromArgb(&H4E, &HA8, &HFF)
    ''' <summary>高台的渲染颜色。</summary>
    Public ReadOnly Property PlatformColor As Color = Color.FromArgb(&H2E, &H7B, &HE0)
    ''' <summary>地面颜色。</summary>
    Public ReadOnly Property GroundColor As Color = Color.FromArgb(&H17, &H1E, &H28)
    ''' <summary>网格线颜色。</summary>
    Public ReadOnly Property GridColor As Color = Color.FromArgb(&H27, &H33, &H44)

    ' ---------- 关卡尺寸常量 ----------
    Private Const GroundMinX As Double = -14.0
    Private Const GroundMaxX As Double = 32.0
    Private Const GroundMinZ As Double = -12.0
    Private Const GroundMaxZ As Double = 12.0
    Private Const ObstacleWidth As Double = 0.5
    Private Const ObstacleDepth As Double = 6.0
    Private Const StepCount As Integer = 4
    Private Const StepDepth As Double = 0.9
    Private Const StepRise As Double = 0.22
    Private Const StepWidth As Double = 6.0
    Private Const PlatformLength As Double = 8.4

    ''' <summary>
    ''' 构建默认关卡并把它注册到 <paramref name="world"/>。
    ''' </summary>
    Public Function BuildDefault(world As PhysicsWorld3D) As Level
        Dim level As New Level With {
            .ObstacleX = 8.0,
            .ObstacleHeight = 0.36,
            .StairsStartX = 14.0,
            .StairsTopY = StepCount * StepRise
        }
        ' 摩擦系数取得比现实偏高：主动布娃娃靠"质心回中"反射维持平衡，
        ' 需要地面提供足够大的水平反力，否则脚会打滑、反射失效。
        Dim groundMat As New PhysicsMaterial(1.4, 0.0)
        Dim solidMat As New PhysicsMaterial(1.2, 0.0)

        ' ---------- 地面 ----------
        Dim ground As RigidBody3D = PhysicsWorld3D.GroundPlane(0.0, groundMat)

        ground.Label = "ground"
        world.Add(ground)
        level.Bodies.Add(ground)

        Call level.Mesh.AddGroundQuad(
            New Vec3(GroundMinX, 0, GroundMinZ),
            New Vec3(GroundMaxX, 0, GroundMinZ),
            New Vec3(GroundMaxX, 0, GroundMaxZ),
            New Vec3(GroundMinX, 0, GroundMaxZ),
            GroundColor)
        Call level.Mesh.AddGrid(GroundMinX, GroundMaxX, GroundMinZ, GroundMaxZ, 2.0, 0.002, GridColor)

        ' ---------- 低矮障碍（跨越） ----------
        Dim obstacle As RigidBody3D = PhysicsWorld3D.StaticBox(
            ObstacleWidth, level.ObstacleHeight, ObstacleDepth,
            New Vector3(level.ObstacleX, level.ObstacleHeight * 0.5, 0), solidMat)

        obstacle.Label = "obstacle"
        world.Add(obstacle)
        level.Bodies.Add(obstacle)

        Call level.Mesh.AddBox(
            New Vec3(level.ObstacleX, level.ObstacleHeight * 0.5, 0),
            New Vec3(ObstacleWidth, level.ObstacleHeight, ObstacleDepth),
            ObstacleColor)

        ' ---------- 台阶 ----------
        For i As Integer = 0 To StepCount - 1
            Dim height As Double = StepRise * (i + 1)
            Dim cx As Double = level.StairsStartX + StepDepth * 0.5 + i * StepDepth
            Dim body As RigidBody3D = PhysicsWorld3D.StaticBox(
                StepDepth, height, StepWidth,
                New Vector3(cx, height * 0.5, 0), solidMat)

            body.Label = $"step{i}"
            world.Add(body)
            level.Bodies.Add(body)

            Call level.Mesh.AddBox(
                New Vec3(cx, height * 0.5, 0),
                New Vec3(StepDepth, height, StepWidth),
                StairColor)
        Next

        ' ---------- 台阶顶部平台 ----------
        Dim platformStartX As Double = level.StairsStartX + StepCount * StepDepth
        Dim platformCenterX As Double = platformStartX + PlatformLength * 0.5
        Dim platform As RigidBody3D = PhysicsWorld3D.StaticBox(
            PlatformLength, level.StairsTopY, StepWidth,
            New Vector3(platformCenterX, level.StairsTopY * 0.5, 0), solidMat)

        platform.Label = "platform"
        world.Add(platform)
        level.Bodies.Add(platform)

        Call level.Mesh.AddBox(
            New Vec3(platformCenterX, level.StairsTopY * 0.5, 0),
            New Vec3(PlatformLength, level.StairsTopY, StepWidth),
            PlatformColor)

        level.StartPosition = New Vec3(-2.0, 0, 0)
        ' heading = π/2 → forward = (sin h, 0, cos h) = +X，正对障碍与台阶
        level.StartHeading = std.PI / 2.0

        Return level
    End Function
End Module
