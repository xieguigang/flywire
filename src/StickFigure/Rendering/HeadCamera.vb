Imports System.Drawing
Imports System.Math
Imports std = System.Math

''' <summary>
''' 针孔相机：用于火柴人的头部第一人称视角。
''' </summary>
''' <remarks>
''' <c>DxScene3DCanvas</c> 的相机是欧拉角轨道模型（眼点恒在场景原点、恒看向原点，
''' <c>position</c> 属性渲染路径并不读取），无法表达"眼点 = 头部位置"，
''' 因此头部视角用这里的手写 view + 透视投影，直接画到 <c>IGraphics</c> 上。
'''
''' 相机空间约定：X 向右、Y 向上、Z 向前（深度）。屏幕坐标 Y 轴向下。
''' </remarks>
Public Class HeadCamera

    ''' <summary>眼点（世界坐标）。</summary>
    Public Property Eye As Vec3 = New Vec3(0, 1.7, 0)
    ''' <summary>视线方向（不必是单位向量）。</summary>
    Public Property Forward As Vec3 = New Vec3(0, 0, 1)
    ''' <summary>上方向参考（不必与视线严格正交）。</summary>
    Public Property Up As Vec3 = New Vec3(0, 1, 0)

    ''' <summary>垂直视场角（度）。</summary>
    Public Property FovDegrees As Double = 72.0

    ''' <summary>画面宽度（像素）。</summary>
    Public Property Width As Integer = 320
    ''' <summary>画面高度（像素）。</summary>
    Public Property Height As Integer = 240

    ''' <summary>近平面距离，小于该深度的顶点被裁剪。</summary>
    Public Property Near As Double = 0.06
    ''' <summary>远平面距离，超过该深度的图元被跳过。</summary>
    Public Property Far As Double = 90.0

    ' 缓存的正交基，由 Prepare() 计算
    Private axisRight As Vec3 = New Vec3(1, 0, 0)
    Private axisUp As Vec3 = New Vec3(0, 1, 0)
    Private axisForward As Vec3 = New Vec3(0, 0, 1)
    Private focal As Double = 1.0

    ''' <summary>由视线与上方向重算正交基与焦距。每帧渲染前调用一次。</summary>
    Public Sub Prepare()
        axisForward = Forward.Normalize()

        Dim upRef As Vec3 = Up

        If Vec3.Cross(upRef, axisForward).LengthSquared < 1.0E-8 Then
            upRef = New Vec3(0, 0, 1)
            If Vec3.Cross(upRef, axisForward).LengthSquared < 1.0E-8 Then
                upRef = New Vec3(1, 0, 0)
            End If
        End If

        ' right = forward × up，使 (right, up, forward) 与屏幕坐标（Y 向下）自洽
        axisRight = Vec3.Cross(axisForward, upRef).Normalize()
        axisUp = Vec3.Cross(axisRight, axisForward).Normalize()

        Dim halfFov As Double = std.Max(1.0, FovDegrees) * std.PI / 360.0

        focal = (Height * 0.5) / std.Tan(halfFov)
    End Sub

    ''' <summary>世界坐标 → 相机坐标（X 右、Y 上、Z 深度）。</summary>
    Public Function ToCamera(p As Vec3) As Vec3
        Dim d As Vec3 = p - Eye

        Return New Vec3(Vec3.Dot(d, axisRight), Vec3.Dot(d, axisUp), Vec3.Dot(d, axisForward))
    End Function

    ''' <summary>相机坐标 → 屏幕像素坐标。</summary>
    Public Function ProjectCamera(c As Vec3) As PointF
        Dim invZ As Double = 1.0 / std.Max(c.Z, 1.0E-6)

        Return New PointF(
            CSng(Width * 0.5 + c.X * focal * invZ),
            CSng(Height * 0.5 - c.Y * focal * invZ))
    End Function

    ''' <summary>世界坐标 → 屏幕像素坐标；深度 ≤ <see cref="Near"/> 时返回 False。</summary>
    Public Function Project(p As Vec3, ByRef screen As PointF) As Boolean
        Dim c As Vec3 = ToCamera(p)

        If c.Z <= Near Then
            screen = PointF.Empty
            Return False
        End If

        screen = ProjectCamera(c)
        Return True
    End Function

    ''' <summary>
    ''' 把一个三维点投到相机空间并判断是否可见（在近远平面之间、且在画面内留有余量）。
    ''' </summary>
    Public Function TryToCamera(p As Vec3, ByRef c As Vec3) As Boolean
        c = ToCamera(p)

        Return c.Z > Near AndAlso c.Z < Far
    End Function

    ''' <summary>
    ''' 把线段裁剪到近平面之后并投影。两端都在近平面之后才绘制。
    ''' </summary>
    Public Function ProjectSegment(a As Vec3, b As Vec3,
                                   ByRef pa As PointF, ByRef pb As PointF) As Boolean
        Dim ca As Vec3 = ToCamera(a)
        Dim cb As Vec3 = ToCamera(b)

        If ca.Z <= Near AndAlso cb.Z <= Near Then
            Return False
        End If

        If ca.Z <= Near Then
            Dim t As Double = (Near - ca.Z) / (cb.Z - ca.Z)
            ca = ca + (cb - ca) * t
        ElseIf cb.Z <= Near Then
            Dim t As Double = (Near - cb.Z) / (ca.Z - cb.Z)
            cb = cb + (ca - cb) * t
        End If

        If ca.Z > Far AndAlso cb.Z > Far Then
            Return False
        End If

        pa = ProjectCamera(ca)
        pb = ProjectCamera(cb)

        Return True
    End Function

    ''' <summary>把相机的眼点 / 朝向设置成"位于 <paramref name="eye"/>，看向 <paramref name="lookAt"/>。</summary>
    Public Sub LookAt(eye As Vec3, lookAt As Vec3, up As Vec3)
        Me.Eye = eye
        Me.Up = up
        Me.Forward = (lookAt - eye)

        If Me.Forward.LengthSquared < 1.0E-8 Then
            Me.Forward = New Vec3(0, 0, 1)
        End If

        Call Prepare()
    End Sub
End Class
