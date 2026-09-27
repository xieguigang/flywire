Imports System.Drawing
Imports Microsoft.VisualBasic.Imaging.Physics
Imports Microsoft.VisualBasic.Imaging.Physics.Collision3D

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

    ''' <summary>由朝向角求单位前向量。</summary>
    Public Shared Function HeadingToForward(heading As Double) As Vec3
        Return New Vec3(Math.Cos(heading), 0, Math.Sin(heading))
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
        Dim groundMat As New PhysicsMaterial(0.9, 0.0)
        Dim solidMat As New PhysicsMaterial(0.8, 0.0)

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

        Return level
    End Function
End Module
