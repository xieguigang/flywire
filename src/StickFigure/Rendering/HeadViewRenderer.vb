Imports System.Drawing
Imports System.Math
Imports Microsoft.VisualBasic.Drawing.DirectX
Imports Microsoft.VisualBasic.Imaging
Imports ImagingBitmap = Microsoft.VisualBasic.Imaging.Bitmap
Imports ImagingPen = Microsoft.VisualBasic.Imaging.Pen
Imports ImagingSolidBrush = Microsoft.VisualBasic.Imaging.SolidBrush
Imports std = System.Math

''' <summary>
''' 头部（第一人称）视角渲染器：把关卡几何与火柴人骨架用自写的针孔投影画到
''' <see cref="IGraphics"/> 上。
''' </summary>
''' <remarks>
''' 之所以不使用 <c>DxScene3DCanvas</c>：它的相机是欧拉角轨道模型，眼点被钉在场景质心，
''' 无法把相机放到火柴人的头部。这里用 <see cref="HeadCamera"/> 做 view + 透视投影，
''' 再用画家算法（按深度从远到近）把三角面填到 Direct2D 画布上，
''' 因此既可以直接画到可见的 <c>DxCanvas</c>，也可以画到离屏的 <see cref="DxGraphics"/>
''' 并用 <c>GetRasterImage()</c> 回读像素，供神经网络取画面输入。
''' </remarks>
Public Class HeadViewRenderer

    ''' <summary>头部相机。</summary>
    Public ReadOnly Property Camera As New HeadCamera()

    ''' <summary>背景色。</summary>
    Public Property BackgroundColor As Color = Color.FromArgb(&H0F, &H14, &H1C)

    ''' <summary>光照方向（指向光源的单位向量）。</summary>
    Public Property LightDirection As Vec3 = New Vec3(-0.45, 0.78, 0.44).Normalize()

    ''' <summary>环境光强度 [0,1]。</summary>
    Public Property Ambient As Double = 0.38

    ''' <summary>雾化起始深度。</summary>
    Public Property FogStart As Double = 14.0
    ''' <summary>雾化终止深度（超过该深度完全融进背景）。</summary>
    Public Property FogEnd As Double = 80.0

    ''' <summary>是否绘制地面网格。</summary>
    Public Property ShowGrid As Boolean = True

    ''' <summary>是否绘制火柴人自身（第一人称下能看到自己的手臂 / 腿）。</summary>
    Public Property ShowSelfBody As Boolean = True

    Private Structure FaceEntry
        Public A As Vec3
        Public B As Vec3
        Public C As Vec3
        Public Depth As Double
        Public Color As Color
    End Structure

    Private ReadOnly faces As New List(Of FaceEntry)()

    ''' <summary>
    ''' 渲染一帧到 <paramref name="g"/>。画布尺寸取自 <see cref="HeadCamera.Width/Height"/>。
    ''' </summary>
    Public Sub Render(g As IGraphics, mesh As LevelMesh, figureLines As IEnumerable(Of LevelLine))
        If g Is Nothing OrElse mesh Is Nothing Then
            Return
        End If

        Call g.Clear(BackgroundColor)
        Call Camera.Prepare()

        faces.Clear()

        ' ---------- 收集三角面 ----------
        Call CollectFaces(mesh.Ground)
        Call CollectFaces(mesh.Solids)

        ' 画家算法：远的先画
        faces.Sort(Function(x, y) y.Depth.CompareTo(x.Depth))

        For Each f As FaceEntry In faces
            Dim pa As PointF = Camera.ProjectCamera(f.A)
            Dim pb As PointF = Camera.ProjectCamera(f.B)
            Dim pc As PointF = Camera.ProjectCamera(f.C)

            If Not InScreen(pa, pb, pc) Then
                Continue For
            End If

            Call g.FillPolygon(New ImagingSolidBrush(f.Color), {pa, pb, pc})
        Next

        ' ---------- 线段 ----------
        If ShowGrid Then
            Call DrawLines(g, mesh.GridLines, 1.0F)
        End If

        If ShowSelfBody AndAlso figureLines IsNot Nothing Then
            Call DrawLines(g, figureLines, 1.6F)
        End If
    End Sub

    Private Sub CollectFaces(source As List(Of LevelTriangle))
        Dim ca As Vec3, cb As Vec3, cc As Vec3

        For Each t As LevelTriangle In source
            If Not Camera.TryToCamera(t.A, ca) Then Continue For
            If Not Camera.TryToCamera(t.B, cb) Then Continue For
            If Not Camera.TryToCamera(t.C, cc) Then Continue For

            Dim depth As Double = (ca.Z + cb.Z + cc.Z) / 3.0

            If depth >= FogEnd Then
                Continue For
            End If

            faces.Add(New FaceEntry With {
                .A = ca,
                .B = cb,
                .C = cc,
                .Depth = depth,
                .Color = Shade(t)
            })
        Next
    End Sub

    ''' <summary>Lambert 明暗 + 距离雾化。</summary>
    Private Function Shade(t As LevelTriangle) As Color
        Dim n As Vec3 = t.Normal
        Dim diffuse As Double = 0.5

        If n.LengthSquared > 1.0E-12 Then
            diffuse = std.Abs(Vec3.Dot(n.Normalize(), LightDirection))
        End If

        Dim lit As Double = Ambient + (1.0 - Ambient) * diffuse
        Dim litColor As Color = LevelMesh.Mix(BackgroundColor, t.Color, std.Min(1.0, lit * 1.15))
        Dim center As Vec3 = t.Center
        Dim cam As Vec3 = Camera.ToCamera(center)
        Dim fog As Double = 1.0 - Clamp01((cam.Z - FogStart) / std.Max(FogEnd - FogStart, 0.001))

        Return LevelMesh.Mix(BackgroundColor, litColor, fog)
    End Function

    Private Sub DrawLines(g As IGraphics, lines As IEnumerable(Of LevelLine), width As Single)
        Dim pen As ImagingPen = Nothing
        Dim penColor As Color = Color.Empty
        Dim pa As PointF, pb As PointF

        For Each l As LevelLine In lines
            If pen Is Nothing OrElse l.Color <> penColor Then
                pen = New ImagingPen(l.Color, width)
                penColor = l.Color
            End If

            If Camera.ProjectSegment(l.A, l.B, pa, pb) Then
                Call g.DrawLine(pen, pa, pb)
            End If
        Next
    End Sub

    ''' <summary>三个顶点是否至少有一部分落在画面内（留 40px 余量）。</summary>
    Private Function InScreen(a As PointF, b As PointF, c As PointF) As Boolean
        Const margin As Single = 40.0F
        Dim left As Single = -margin
        Dim right As Single = Camera.Width + margin
        Dim top As Single = -margin
        Dim bottom As Single = Camera.Height + margin

        Return PointNear(a, left, right, top, bottom) OrElse
               PointNear(b, left, right, top, bottom) OrElse
               PointNear(c, left, right, top, bottom)
    End Function

    Private Shared Function PointNear(p As PointF, left As Single, right As Single,
                                      top As Single, bottom As Single) As Boolean
        Return p.X >= left AndAlso p.X <= right AndAlso p.Y >= top AndAlso p.Y <= bottom
    End Function

    Private Shared Function Clamp01(v As Double) As Double
        Return std.Min(1.0, std.Max(0.0, v))
    End Function

    ' /********************************************************************************/
    '  取帧（神经网络输入）
    ' /********************************************************************************/

    ''' <summary>
    ''' 离屏渲染一帧并返回位图。不需要可见的 UI，可在无头训练循环中调用。
    ''' </summary>
    Public Function Capture(width As Integer, height As Integer,
                            mesh As LevelMesh,
                            figureLines As IEnumerable(Of LevelLine)) As ImagingBitmap
        If width <= 0 OrElse height <= 0 Then
            Return Nothing
        End If

        Camera.Width = width
        Camera.Height = height

        Using g As New DxGraphics(width, height, BackgroundColor)
            Call Render(g, mesh, figureLines)

            Return g.GetRasterImage()
        End Using
    End Function

    ''' <summary>
    ''' 直接取回灰度张量（行优先，长度 = w·h，取值 0..255），供神经网络使用。
    ''' </summary>
    ''' <remarks>
    ''' 像素缓冲区布局为 BGRA（见 <c>BitmapBuffer</c>），因此
    ''' R=+2、G=+1、B=+0、A=+3。
    ''' </remarks>
    Public Function CaptureGray(width As Integer, height As Integer,
                                mesh As LevelMesh,
                                figureLines As IEnumerable(Of LevelLine)) As Byte()
        Dim image As ImagingBitmap = Capture(width, height, mesh, figureLines)

        If image Is Nothing Then
            Return New Byte(width * height - 1) {}
        End If

        Dim buffer As Byte() = image.MemoryBuffer.RawBuffer

        If buffer Is Nothing OrElse buffer.Length < width * height * 4 Then
            Return New Byte(width * height - 1) {}
        End If

        Dim gray(width * height - 1) As Byte

        For i As Integer = 0 To gray.Length - 1
            Dim b As Integer = buffer(i * 4)
            Dim g As Integer = buffer(i * 4 + 1)
            Dim r As Integer = buffer(i * 4 + 2)

            gray(i) = CByte(std.Min(255, CInt(0.299 * r + 0.587 * g + 0.114 * b)))
        Next

        Return gray
    End Function

End Class
