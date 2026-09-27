Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.VisualBasic.Drawing.DirectX
Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D
Imports Microsoft.VisualBasic.Imaging.Drawing3D
Imports ImagingBitmap = Microsoft.VisualBasic.Imaging.Bitmap

''' <summary>
''' 主视角：封装 <see cref="DxScene3DCanvas"/> 的自由轨道相机 3D 场景。
''' </summary>
''' <remarks>
''' 关键约束：<c>Scene</c> 每次装载几何都会把顶点按"自身质心"重新居中。
''' 因此静态关卡面只在 <see cref="LoadLevel"/> 时装载一次（此后质心固定），
''' 火柴人则每帧通过 <c>UpdateConnections</c> 提交线段——连线只做同样的居中平移，
''' 不会反过来移动已经装载好的静态面，人物走到哪里场景都不会整体漂移。
'''
''' 鼠标交互由控件自带的 <c>OrbitCameraController</c> 提供：
''' 左键旋转、右键平移、滚轮缩放，另有 R/F/M/G/C/D/S 等快捷键。
''' </remarks>
Public Class OrbitView

    Private ReadOnly m_canvas As DxScene3DCanvas

    ''' <summary>底层的 DirectX 场景控件。</summary>
    Public ReadOnly Property Canvas As DxScene3DCanvas
        Get
            Return m_canvas
        End Get
    End Property

    ''' <summary>是否让相机跟随火柴人（用屏幕空间平移把人物保持在画面中心）。</summary>
    Public Property FollowFigure As Boolean = True

    ''' <summary>是否绘制关节十字标记。</summary>
    Public Property ShowJoints As Boolean = True

    ''' <summary>是否绘制地面网格。</summary>
    Public Property ShowGrid As Boolean = True

    ''' <summary>已经装载的静态网格线（地面网格），每帧与骨架线合并提交。</summary>
    Private ReadOnly baseLines As New List(Of LevelLine)()

    ''' <summary>当前是否有关卡被装载。</summary>
    Public Property HasLevel As Boolean = False

    Public Sub New()
        m_canvas = New DxScene3DCanvas With {
            .Dock = DockStyle.Fill,
            .AutoClear = False,
            .BackColor = Color.FromArgb(&H0F, &H14, &H1C),
            .BackgroundColor = Color.FromArgb(&H0F, &H14, &H1C),
            .RenderMode = SceneRenderMode.Surface,
            .ShowGround = True,
            .ShowConnections = True,
            .ShowDebugOverlay = False,
            .EnableKeyboardShortcuts = True,
            .MultisampleCount = 4,
            .CullBackFaces = False,
            .VSync = True
        }

        m_canvas.Lighting.Azimuth = -35
        m_canvas.Lighting.Elevation = 55
        m_canvas.Lighting.Ambient = 42
        m_canvas.Lighting.Intensity = 70
        m_canvas.Lighting.LightColor = Color.White
    End Sub

    ''' <summary>
    ''' 装载静态关卡。<see cref="DxScene3DCanvas"/> 没有内部渲染循环，
    ''' 需要宿主用定时器周期性调用 <see cref="DxScene3DCanvas.RequestRender"/>。
    ''' </summary>
    Public Sub LoadLevel(level As Level)
        baseLines.Clear()

        If ShowGrid Then
            Call baseLines.AddRange(level.Mesh.GridLines)
        End If

        Call m_canvas.LoadSurfaces(level.Mesh.ToSurfaces())
        Call m_canvas.UpdateConnections(FigureVisual.ToLineSegments(baseLines))

        HasLevel = True

        ' 手动设置一个合适的初始视角：FitView 会把整个关卡（跨度 30m）缩得很小
        Call ResetCamera()
    End Sub

    ''' <summary>恢复默认视角（俯视 16°、方位 −40°、视距 20）。</summary>
    Public Sub ResetCamera()
        Dim cam As Camera = m_canvas.Controller.Camera

        m_canvas.UpdateViewport()

        cam.FieldOfView = 2048.0F
        cam.ViewDistance = 20.0F
        cam.Offset = New PointF(0, 0)
        cam.AngleX = 16.0F
        cam.AngleY = -40.0F
        cam.AngleZ = 0.0F

        Call m_canvas.Lighting.ApplyTo(cam)
        Call m_canvas.RequestRender()
    End Sub

    ''' <summary>
    ''' 更新火柴人的骨架线段并请求重绘。
    ''' </summary>
    Public Sub UpdateFigure(skel As StickmanSkeleton, Optional followPoint As Vec3? = Nothing)
        If Not HasLevel OrElse skel Is Nothing Then
            Return
        End If

        Dim lines As List(Of LevelLine) = FigureVisual.BuildFigureLines(skel, includeJoints:=ShowJoints)

        If ShowGrid Then
            Call lines.InsertRange(0, baseLines)
        End If

        Call m_canvas.UpdateConnections(FigureVisual.ToLineSegments(lines))

        If FollowFigure Then
            Dim target As Vec3 = If(followPoint.HasValue, followPoint.Value, skel.PelvisPosition)

            Call Follow(target)
        End If

        Call m_canvas.RequestRender()
    End Sub

    ''' <summary>
    ''' 用屏幕空间平移把 <paramref name="worldPoint"/> 拉回画面中心。
    ''' </summary>
    ''' <remarks>
    ''' 相机模型没有 look-at（眼点恒在场景原点），所以"跟随"只能通过
    ''' <c>Camera.Offset</c> 做画面平移实现；由于 offset 在投影里是线性叠加的，
    ''' 一次修正即可收敛。
    ''' </remarks>
    Public Sub Follow(worldPoint As Vec3)
        Dim cam As Camera = m_canvas.Controller.Camera
        Dim scenePoint As Point3D = FigureVisual.ToScene(worldPoint) - m_canvas.Scene.Center
        Dim screen As PointF

        If Not m_canvas.TryProjectPoint(scenePoint, screen) Then
            Return
        End If

        cam.Offset = New PointF(
            cam.Offset.X + (m_canvas.Width * 0.5F - screen.X),
            cam.Offset.Y + (m_canvas.Height * 0.5F - screen.Y))
    End Sub

    ''' <summary>截取主视角画面。</summary>
    Public Function Snapshot() As ImagingBitmap
        Return m_canvas.Snapshot()
    End Function

    ''' <summary>把主视角画面存成图片。</summary>
    Public Function SaveSnapshot(file As String) As Boolean
        Return m_canvas.SaveSnapshot(file, Microsoft.VisualBasic.Imaging.ImageFormats.Png)
    End Function
End Class
