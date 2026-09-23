Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualBasic.Drawing.DirectX
Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D
Imports Microsoft.VisualBasic.Imaging.Drawing2D.Colors
Imports Neuropils.Data
Imports Neuropils.Rendering

''' <summary>
''' 果蝇大脑三维可视化主窗体。
''' </summary>
''' <remarks>
''' 界面分四块：
''' 
''' * 顶部菜单与工具条：数据目录、着色维度、连接模式、强度阈值、点大小、开关与截图；
''' * 中间左侧：<see cref="DxScene3DCanvas"/> (DirectX 11 三维画布)；
''' * 中间右侧：图例 (兼作按脑区/细胞类型/递质的显示筛选) 与神经元详情；
''' * 底部状态栏：当前数据集规模、装配耗时与后台任务的进度条。
''' 
''' 两个后台线程分别负责"读表" (<see cref="BrainDatasetLoader"/>) 与"装配场景"
''' (<see cref="BrainSceneBuilder"/>)：534 万条连接的过滤与连线构建需要数百毫秒，
''' 放在 UI 线程上会明显卡顿。画布更新回落到 UI 线程执行。
''' </remarks>
Public Partial Class FormMain

    ''' <summary>数据目录的默认位置 (与 SNN 仿真使用的是同一份 FAFB v783 数据)。</summary>
    Private Const DefaultDataDir As String = "F:\flywire\FAFB-v783"

    Private ReadOnly m_config As New VisualizationConfig()
    Private ReadOnly m_renderer As New Direct3D11SceneRenderer()
    Private ReadOnly m_buildOptions As New SceneBuildOptions()

    Private m_canvas As DxScene3DCanvas
    Private m_legend As CheckedListBox
    Private m_gradient As PictureBox
    Private m_details As TextBox
    Private m_dimensionBox As ToolStripComboBox
    Private m_connectionBox As ToolStripComboBox
    Private m_lineColorBox As ToolStripComboBox
    Private m_thresholdBox As NumericUpDown
    Private m_pointSizeBox As NumericUpDown
    Private m_showConnections As CheckBox
    Private m_showGround As CheckBox
    Private m_progress As ToolStripProgressBar
    Private m_statusText As ToolStripStatusLabel
    Private m_sceneText As ToolStripStatusLabel

    Private m_dataset As BrainDataset
    Private m_colorizer As NeuronColorizer
    Private m_scene As BrainScene
    Private m_lookup As Integer()
    Private m_focusNeuron As Integer = -1
    Private m_viewInitialized As Boolean = False
    Private m_cancel As CancellationTokenSource
    Private m_busy As Boolean

    ''' <summary>
    ''' 拖动 / 筛选滑块的防抖定时器 (面板"应用"一次而不是每帧重建)。
    ''' </summary>
    ''' <remarks>
    ''' 必须写全 System.Windows.Forms.Timer：System.Threading 里也有一个同名的 Timer
    ''' (那个在多线程上触发回调，拿来更新界面会直接踩到跨线程访问)。
    ''' </remarks>
    Private ReadOnly m_rebuildTimer As New System.Windows.Forms.Timer()

    Public Sub New()
        Call initializeUi()

        Call m_rebuildTimer.Stop()

        m_rebuildTimer.Interval = 260
        AddHandler m_rebuildTimer.Tick, AddressOf onRebuildTimerTick
    End Sub

#Region "ui construction"

    Private Sub initializeUi()
        Me.Text = "Neuropils - Drosophila brain 3D viewer"
        Me.ClientSize = New Size(1500, 900)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.MinimumSize = New Size(900, 600)

        m_canvas = New DxScene3DCanvas With {
            .Dock = DockStyle.Fill,
            .AutoClear = False,
            .BackColor = Color.Black,
            .Renderer = m_renderer,
            .RenderMode = SceneRenderMode.PointCloud,
            .ColorScheme = "viridis",
            .UseEmbeddedColor = True,
            .ShowConnections = False,
            .ShowGround = False,
            .PointSize = 2,
            .PointAlpha = 255,
            .MultisampleCount = 1,
            .CullBackFaces = False,
            .EnableKeyboardShortcuts = True
        }

        ' 点击拾取：画布的鼠标事件先喂给相机控制器，这里只在"没有拖动"时当作点击
        AddHandler m_canvas.MouseDown, AddressOf onCanvasMouseDown
        AddHandler m_canvas.MouseUp, AddressOf onCanvasMouseUp

        Dim sidebar As Control = createSidebar()

        Dim split As New SplitContainer With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
            .SplitterDistance = 1130,
            .FixedPanel = FixedPanel.Panel2,
            .Panel1MinSize = 400,
            .Panel2MinSize = 260
        }

        split.Panel1.Controls.Add(m_canvas)
        split.Panel2.Controls.Add(sidebar)

        Me.Controls.Add(split)
        Me.Controls.Add(createToolbar())
        Me.Controls.Add(createMenu())
        Me.Controls.Add(createStatusBar())

        Call refreshLegend()
    End Sub

    Private Function createMenu() As MenuStrip
        Dim menu As New MenuStrip()

        Dim fileMenu As New ToolStripMenuItem("文件 (&F)")
        Dim openItem As New ToolStripMenuItem("打开数据目录 (&O)...")
        Dim snapshotItem As New ToolStripMenuItem("保存截图 (&S)...")
        Dim exitItem As New ToolStripMenuItem("退出 (&X)")

        AddHandler openItem.Click, AddressOf onOpenDataDir
        AddHandler snapshotItem.Click, AddressOf onSnapshot
        AddHandler exitItem.Click, Sub(sender As Object, e As EventArgs) Call Me.Close()

        Call fileMenu.DropDownItems.Add(openItem)
        Call fileMenu.DropDownItems.Add(snapshotItem)
        Call fileMenu.DropDownItems.Add(New ToolStripSeparator())
        Call fileMenu.DropDownItems.Add(exitItem)

        Dim viewMenu As New ToolStripMenuItem("视图 (&V)")
        Dim resetItem As New ToolStripMenuItem("重置视角 (&R)")

        AddHandler resetItem.Click, Sub(sender As Object, e As EventArgs) Call m_canvas.ResetView()

        Call viewMenu.DropDownItems.Add(resetItem)

        Dim helpMenu As New ToolStripMenuItem("帮助 (&H)")
        Dim aboutItem As New ToolStripMenuItem("关于数据来源 (&A)")

        AddHandler aboutItem.Click, AddressOf onAbout

        Call helpMenu.DropDownItems.Add(aboutItem)

        Call menu.Items.Add(fileMenu)
        Call menu.Items.Add(viewMenu)
        Call menu.Items.Add(helpMenu)

        Return menu
    End Function

    Private Function createToolbar() As ToolStrip
        Dim bar As New ToolStrip With {
            .GripStyle = ToolStripGripStyle.Hidden,
            .ImageScalingSize = New Size(16, 16)
        }

        m_dimensionBox = New ToolStripComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 150}
        Call m_dimensionBox.Items.AddRange(New Object() {
            "主导脑区 (neuropil)", "神经递质", "细胞类型", "分类层级", "仿真活跃度"
        })
        m_dimensionBox.SelectedIndex = 0
        AddHandler m_dimensionBox.SelectedIndexChanged, AddressOf onDimensionChanged

        m_connectionBox = New ToolStripComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 150}
        Call m_connectionBox.Items.AddRange(New Object() {
            "连接 (逐条)", "脑区宏连接", "选中神经元的连接"
        })
        m_connectionBox.SelectedIndex = 0
        AddHandler m_connectionBox.SelectedIndexChanged, AddressOf onConnectionModeChanged

        m_lineColorBox = New ToolStripComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 130}
        Call m_lineColorBox.Items.AddRange(New Object() {
            "连线: 前突触颜色", "连线: 递质类型", "连线: 单色"
        })
        m_lineColorBox.SelectedIndex = 0
        AddHandler m_lineColorBox.SelectedIndexChanged, AddressOf onLineColorChanged

        ' WinForms 没有 ToolStripNumericUpDown：数值输入要自己用 ToolStripControlHost 托住
        m_thresholdBox = New NumericUpDown With {
            .Minimum = 1, .Maximum = 100000, .Increment = 10, .Value = m_buildOptions.SynapseThreshold, .Width = 80
        }
        AddHandler m_thresholdBox.ValueChanged, AddressOf onThresholdChanged

        m_pointSizeBox = New NumericUpDown With {
            .Minimum = 1, .Maximum = 12, .Increment = 1, .Value = 2, .Width = 56
        }
        AddHandler m_pointSizeBox.ValueChanged, AddressOf onPointSizeChanged

        m_showConnections = New ToolStripButton("显示连接") With {.CheckOnClick = True, .Checked = False}
        AddHandler m_showConnections.CheckedChanged, AddressOf onShowConnectionsChanged

        m_showGround = New ToolStripButton("地面") With {.CheckOnClick = True, .Checked = False}
        AddHandler m_showGround.CheckedChanged, AddressOf onShowGroundChanged

        Dim snapshot As New ToolStripButton("截图")
        AddHandler snapshot.Click, AddressOf onSnapshot

        Dim reload As New ToolStripButton("重新载入")
        AddHandler reload.Click, AddressOf onReload

        Call bar.Items.Add(New ToolStripLabel("着色:"))
        Call bar.Items.Add(m_dimensionBox)
        Call bar.Items.Add(New ToolStripSeparator())
        Call bar.Items.Add(New ToolStripLabel("连接:"))
        Call bar.Items.Add(m_connectionBox)
        Call bar.Items.Add(m_lineColorBox)
        Call bar.Items.Add(New ToolStripLabel("≥突触:"))
        Call bar.Items.Add(New ToolStripControlHost(m_thresholdBox))
        Call bar.Items.Add(New ToolStripSeparator())
        Call bar.Items.Add(New ToolStripLabel("点大小:"))
        Call bar.Items.Add(New ToolStripControlHost(m_pointSizeBox))
        Call bar.Items.Add(m_showConnections)
        Call bar.Items.Add(m_showGround)
        Call bar.Items.Add(New ToolStripSeparator())
        Call bar.Items.Add(snapshot)
        Call bar.Items.Add(reload)

        Return bar
    End Function

    Private Function createSidebar() As Control
        Dim panel As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .Padding = New Padding(6)
        }

        Call panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24))
        Call panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 26))
        Call panel.RowStyles.Add(New RowStyle(SizeType.Percent, 45))
        Call panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24))
        Call panel.RowStyles.Add(New RowStyle(SizeType.Percent, 55))

        Call panel.Controls.Add(newLabel("图例 / 筛选 (勾选控制显示)"), 0, 0)

        m_gradient = New PictureBox With {.Dock = DockStyle.Fill, .Height = 20, .Visible = False, .SizeMode = PictureBoxSizeMode.StretchImage}

        Call panel.Controls.Add(m_gradient, 0, 1)

        m_legend = New CheckedListBox With {
            .Dock = DockStyle.Fill,
            .CheckOnClick = True,
            .IntegralHeight = False
        }
        AddHandler m_legend.ItemCheck, AddressOf onLegendItemCheck

        Call panel.Controls.Add(m_legend, 0, 2)
        Call panel.Controls.Add(newLabel("神经元详情 (点击画布中的点)"), 0, 3)

        m_details = New TextBox With {
            .Dock = DockStyle.Fill,
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Vertical,
            .Font = New Font("Consolas", 9),
            .BackColor = Color.FromArgb(250, 250, 250)
        }

        Call panel.Controls.Add(m_details, 0, 4)

        Return panel
    End Function

    Private Shared Function newLabel(text As String) As Label
        Return New Label With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9, FontStyle.Bold)
        }
    End Function

    Private Function createStatusBar() As StatusStrip
        Dim bar As New StatusStrip()

        m_statusText = New ToolStripStatusLabel("就绪") With {.Spring = True, .TextAlign = ContentAlignment.MiddleLeft}
        m_sceneText = New ToolStripStatusLabel("") With {.BorderSides = ToolStripStatusLabelBorderSides.Left}
        m_progress = New ToolStripProgressBar With {.Visible = False, .Width = 220, .Style = ProgressBarStyle.Marquee}

        Call bar.Items.Add(m_statusText)
        Call bar.Items.Add(m_sceneText)
        Call bar.Items.Add(m_progress)

        Return bar
    End Function

#End Region

#Region "life cycle"

    Private Sub FormMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        m_config.DataDir = DefaultDataDir

        Dim args As String() = Environment.GetCommandLineArgs()

        ' 自检模式：把数据层与场景装配的实测结果写成报告，便于无人值守的回归
        If args.Length > 1 AndAlso String.Equals(args(1), "--selftest", StringComparison.OrdinalIgnoreCase) Then
            Call runSelfTest(args)

            Return
        End If

        If args.Length > 1 AndAlso Directory.Exists(args(1)) Then
            m_config.DataDir = args(1)
        End If

        Call startLoad()
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        m_rebuildTimer.Stop()

        If m_cancel IsNot Nothing Then
            Call m_cancel.Cancel()
        End If

        Call MyBase.OnFormClosed(e)
    End Sub

#End Region

#Region "data loading"

    Private Sub startLoad()
        If m_busy Then Return

        m_busy = True

        Dim tokenSource As New CancellationTokenSource()

        m_cancel = tokenSource
        m_statusText.Text = $"正在载入 {m_config.DataDir} ..."
        m_progress.Visible = True
        m_dataset = Nothing
        m_viewInitialized = False
        Call m_canvas.ClearScene()

        Dim loader As New BrainDatasetLoader(m_config)

        Task.Run(
            Function() As BrainDataset
                ' 进度回调来自后台线程，切回 UI 线程再更新状态栏
                Return loader.Load(AddressOf onLoadProgress, tokenSource.Token)
            End Function) _
            .ContinueWith(AddressOf onLoadCompleted, TaskScheduler.FromCurrentSynchronizationContext())
    End Sub

    Private Sub onLoadProgress(message As String)
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return

        Call BeginInvoke(New Action(
            Sub()
                m_statusText.Text = message
            End Sub))
    End Sub

    Private Sub onLoadCompleted(task As Task(Of BrainDataset))
        m_busy = False
        m_progress.Visible = False

        If task.IsCanceled Then
            m_statusText.Text = "载入已取消"
            Return
        End If

        If task.IsFaulted Then
            Dim reason As String = If(task.Exception?.GetBaseException()?.Message, "unknown error")

            m_statusText.Text = "载入失败"
            Call MessageBox.Show(Me, reason, "载入数据失败", MessageBoxButtons.OK, MessageBoxIcon.Error)

            Return
        End If

        m_dataset = task.Result
        m_statusText.Text = $"数据载入完成: {m_dataset}"
        m_sceneText.Text = m_dataset.ToString()

        If m_dataset.Units = 0 Then
            Return
        End If

        ' 默认着色维度：主导脑区
        Call rebuildColorizer(NeuronColorDimension.Neuropil, resetUi:=False)
        Call rebuildScene(resetView:=True)
    End Sub

    ''' <summary>重新载入数据目录 (可以在界面上换一台数据集)。</summary>
    Private Sub onReload(sender As Object, e As EventArgs)
        If m_busy Then Return

        Using dialog As New FolderBrowserDialog()
            dialog.Description = "选择 FAFB v783 数据目录"
            dialog.SelectedPath = m_config.DataDir

            If dialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            m_config.DataDir = dialog.SelectedPath
        End Using

        Call startLoad()
    End Sub

    Private Sub onOpenDataDir(sender As Object, e As EventArgs)
        Call onReload(sender, e)
    End Sub

#End Region

#Region "scene assembly"

    ''' <summary>重建着色器与图例。</summary>
    Private Sub rebuildColorizer(dimension As NeuronColorDimension, resetUi As Boolean)
        If m_dataset Is Nothing Then Return

        m_colorizer = NeuronColorizer.Create(m_dataset, dimension)

        If m_colorizer Is Nothing Then Return

        If m_colorizer.IsHeatMap Then
            ' 热力图用渲染管线的调色板纹理，点云改走 Intensity 通道
            m_canvas.UseEmbeddedColor = False
            m_canvas.ColorScheme = "viridis"
            Call showGradient()
        Else
            m_canvas.UseEmbeddedColor = True
            m_gradient.Visible = False
        End If

        If resetUi Then
            Call refreshLegend()
        End If
    End Sub

    ''' <summary>重建点云与连线并送进画布。</summary>
    Private Sub rebuildScene(Optional resetView As Boolean = False)
        If m_dataset Is Nothing OrElse m_colorizer Is Nothing Then Return
        If m_busy Then Return

        m_busy = True
        m_statusText.Text = "正在装配场景 ..."
        m_progress.Visible = True

        If m_buildOptions.Mode = ConnectionRenderMode.SelectedNeuron Then
            m_buildOptions.FocusNeuron = m_focusNeuron
        End If

        Dim options As SceneBuildOptions = m_buildOptions
        Dim dataset As BrainDataset = m_dataset
        Dim colorizer As NeuronColorizer = m_colorizer
        Dim timer As Stopwatch = Stopwatch.StartNew()

        Task.Run(
            Function() As BrainScene
                Return BrainSceneBuilder.Build(dataset, colorizer, options)
            End Function) _
            .ContinueWith(
                Sub(task As Task(Of BrainScene))
                    m_busy = False
                    m_progress.Visible = False

                    If task.IsFaulted Then
                        m_statusText.Text = $"场景装配失败: {If(task.Exception?.GetBaseException()?.Message, "unknown")}"

                        Return
                    End If

                    timer.Stop()

                    m_scene = task.Result
                    m_lookup = m_scene.BuildLookup(dataset.Units)

                    Call applyScene(resetView)

                    m_statusText.Text = $"场景就绪 ({timer.ElapsedMilliseconds} ms): {m_scene.Describe()}"
                    m_sceneText.Text = m_scene.Describe()
                End Sub,
                TaskScheduler.FromCurrentSynchronizationContext())
    End Sub

    ''' <summary>
    ''' 把装配好的场景交给画布。
    ''' </summary>
    ''' <remarks>
    ''' 第一次载入走 <c>LoadPointCloud</c> (会 FitView)，之后的重新着色 / 筛选走
    ''' <c>UpdatePointCloud</c>：用户在交互中调好的视角不应该被一次重新着色重置。
    ''' </remarks>
    Private Sub applyScene(resetView As Boolean)
        If m_scene Is Nothing Then Return

        If resetView OrElse Not m_viewInitialized Then
            Call m_canvas.LoadPointCloud(m_scene.Points)
            Call m_canvas.UpdateConnections(m_scene.Lines)

            m_viewInitialized = True
        Else
            Call m_canvas.UpdatePointCloud(m_scene.Points)

            If m_showConnections.Checked Then
                Call m_canvas.UpdateConnections(m_scene.Lines)
            End If
        End If

        ' 选中标记：在选中神经元周围画一个小十字，任何缩放下都看得见
        If m_focusNeuron >= 0 AndAlso m_focusNeuron < m_dataset.Units AndAlso m_dataset.HasPosition(m_focusNeuron) Then
            Call drawFocusMarker(m_focusNeuron)
        End If
    End Sub

    ''' <summary>在选中神经元周围补三根短十字线 (与连线一起提交)。</summary>
    Private Sub drawFocusMarker(neuron As Integer)
        Dim position As FlywireAI.FAFBv783.NeuronPosition = m_dataset.GetPosition(neuron)
        Dim lines As New List(Of LineSegment)(3)
        Dim size As Double = 6000.0     ' 纳米：约 6 微米，全脑尺度下可见又不夸张
        Dim color As Color = Color.FromArgb(255, 255, 255)

        For axis As Integer = 0 To 2
            Dim a As New Microsoft.VisualBasic.Imaging.Drawing3D.Point3D(position.X, position.Y, position.Z)
            Dim b As New Microsoft.VisualBasic.Imaging.Drawing3D.Point3D(position.X, position.Y, position.Z)

            Select Case axis
                Case 0
                    a.X -= size
                    b.X += size
                Case 1
                    a.Y -= size
                    b.Y += size
                Case Else
                    a.Z -= size
                    b.Z += size
            End Select

            ' 用亮色 + 不透明，穿过点云也能一眼看到
            Call lines.Add(New LineSegment(a, b, color))
        Next

        If m_scene.Lines IsNot Nothing Then
            Call lines.InsertRange(0, m_scene.Lines)
        End If

        Call m_canvas.UpdateConnections(lines)

        m_canvas.ShowConnections = True
        m_showConnections.Checked = True
    End Sub

    Private Sub onRebuildTimerTick(sender As Object, e As EventArgs)
        m_rebuildTimer.Stop()
        Call rebuildScene()
    End Sub

    ''' <summary>请求一次防抖之后的重建 (滑块 / 勾选频繁触发时使用)。</summary>
    Private Sub scheduleRebuild()
        m_rebuildTimer.Stop()
        m_rebuildTimer.Start()
    End Sub

#End Region

#Region "toolbar events"

    Private Sub onDimensionChanged(sender As Object, e As EventArgs)
        If m_dataset Is Nothing Then Return

        Dim dimension As NeuronColorDimension

        Select Case m_dimensionBox.SelectedIndex
            Case 1 : dimension = NeuronColorDimension.Neurotransmitter
            Case 2 : dimension = NeuronColorDimension.CellType
            Case 3 : dimension = NeuronColorDimension.SuperClass
            Case 4 : dimension = NeuronColorDimension.Activity
            Case Else : dimension = NeuronColorDimension.Neuropil
        End Select

        If dimension = NeuronColorDimension.Activity AndAlso Not m_dataset.HasActivity Then
            Call MessageBox.Show(
                Me,
                "当前数据集没有仿真活跃度数据。可以先在 src\test 里跑一次全脑仿真" &
                "（会生成 snn-output\<时间戳>\neuron_activity_*.csv），再重新载入。",
                "缺少活跃度数据", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If

        Call rebuildColorizer(dimension, resetUi:=True)
        Call rebuildScene()
    End Sub

    Private Sub onConnectionModeChanged(sender As Object, e As EventArgs)
        Select Case m_connectionBox.SelectedIndex
            Case 1 : m_buildOptions.Mode = ConnectionRenderMode.NeuropilAggregate
            Case 2 : m_buildOptions.Mode = ConnectionRenderMode.SelectedNeuron
            Case Else : m_buildOptions.Mode = ConnectionRenderMode.PerConnection
        End Select

        Call scheduleRebuild()
    End Sub

    Private Sub onLineColorChanged(sender As Object, e As EventArgs)
        Select Case m_lineColorBox.SelectedIndex
            Case 1 : m_buildOptions.LineColor = LineColorMode.Neurotransmitter
            Case 2 : m_buildOptions.LineColor = LineColorMode.Uniform
            Case Else : m_buildOptions.LineColor = LineColorMode.PresynapticNeuron
        End Select

        Call scheduleRebuild()
    End Sub

    Private Sub onThresholdChanged(sender As Object, e As EventArgs)
        m_buildOptions.SynapseThreshold = CInt(m_thresholdBox.Value)

        ' 阈值只影响连线，点云不动
        Call scheduleRebuild()
    End Sub

    Private Sub onPointSizeChanged(sender As Object, e As EventArgs)
        m_canvas.PointSize = CInt(m_pointSizeBox.Value)
    End Sub

    Private Sub onShowConnectionsChanged(sender As Object, e As EventArgs)
        m_canvas.ShowConnections = m_showConnections.Checked
    End Sub

    Private Sub onShowGroundChanged(sender As Object, e As EventArgs)
        m_canvas.ShowGround = m_showGround.Checked
    End Sub

    Private Sub onSnapshot(sender As Object, e As EventArgs)
        If Not m_viewInitialized Then Return

        Using dialog As New SaveFileDialog()
            dialog.Filter = "PNG 图像|*.png"
            dialog.FileName = $"drosophila_brain_{DateTime.Now:yyyyMMdd_HHmmss}.png"

            If dialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            If m_canvas.SaveSnapshot(dialog.FileName, Microsoft.VisualBasic.Imaging.ImageFormats.Png) Then
                m_statusText.Text = $"截图已保存: {dialog.FileName}"
            Else
                m_statusText.Text = $"截图保存失败: {m_canvas.LastError}"
            End If
        End Using
    End Sub

    Private Sub onAbout(sender As Object, e As EventArgs)
        Call MessageBox.Show(
            Me,
            "数据来源: FlyWire FAFB v783 (codex.flywire.ai)" & Environment.NewLine &
            "神经元数 139,255 / 连接 534 万条 (≥5 突触)" & Environment.NewLine &
            "点云位置来自 coordinates.csv，脑区归属由 neuropil_synapse_table.csv 取 argmax" & Environment.NewLine &
            "渲染: Microsoft.VisualBasic.Drawing (Direct3D 11)" & Environment.NewLine & Environment.NewLine &
            "快捷键: 左键旋转 / 右键平移 / 滚轮缩放 / R 重置 / F 适配 / G 地面 / C 连接 / S 截图",
            "关于 Neuropils", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

#End Region

#Region "legend"

    ''' <summary>刷新图例列表 (热力图维度下显示色标而不是勾选列表)。</summary>
    Private Sub refreshLegend()
        Call m_legend.Items.Clear()

        If m_colorizer Is Nothing Then Return

        If m_colorizer.IsHeatMap Then
            m_legend.Enabled = False
            Call m_legend.Items.Add($"活跃度热力图 (最小 {minActivity():F0} / 最大 {maxActivity():F0} 脉冲)")

            Return
        End If

        m_legend.Enabled = True

        For Each item As ColorLegendItem In m_colorizer.Legend
            Dim index As Integer = m_legend.Items.Add(item)

            m_legend.SetItemChecked(index, item.Visible)
        Next
    End Sub

    Private Sub onLegendItemCheck(sender As Object, e As ItemCheckEventArgs)
        If m_colorizer Is Nothing OrElse m_colorizer.IsHeatMap Then Return
        If e.Index < 0 OrElse e.Index >= m_legend.Items.Count Then Return

        Dim item As ColorLegendItem = TryCast(m_legend.Items(e.Index), ColorLegendItem)

        If item Is Nothing Then Return

        Call m_colorizer.SetVisible(item, e.NewValue = CheckState.Checked)

        ' 勾选状态在事件返回之后才生效，因此重建延后到消息循环的空闲时刻
        Call BeginInvoke(New Action(AddressOf scheduleRebuild))
    End Sub

    Private Function minActivity() As Double
        Return activityRange().Item1
    End Function

    Private Function maxActivity() As Double
        Return activityRange().Item2
    End Function

    Private Function activityRange() As (Double, Double)
        If m_dataset Is Nothing OrElse Not m_dataset.HasActivity Then Return (0.0, 0.0)

        Dim min As Double = Double.MaxValue
        Dim max As Double = Double.MinValue

        For Each value As Double In m_dataset.Activity
            If value < min Then min = value
            If value > max Then max = value
        Next

        Return (min, max)
    End Function

    ''' <summary>画出当前配色方案的色标 (热力图维度)。</summary>
    Private Sub showGradient()
        Dim colors As Color() = Designer.GetColors("viridis", 256, 255)
        Dim bitmap As New Bitmap(256, 1)

        For i As Integer = 0 To 255
            bitmap.SetPixel(i, 0, colors(i))
        Next

        ' 旧的位图要主动释放：切换维度会反复生成色标
        Dim stale As Image = m_gradient.Image

        m_gradient.Image = bitmap
        m_gradient.Visible = True

        If stale IsNot Nothing Then
            stale.Dispose()
        End If
    End Sub

#End Region

#Region "picking"

    Private m_mouseDown As Point
    Private m_mouseDownValid As Boolean

    Private Sub onCanvasMouseDown(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return

        m_mouseDown = New Point(e.X, e.Y)
        m_mouseDownValid = True
    End Sub

    Private Sub onCanvasMouseUp(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left OrElse Not m_mouseDownValid Then Return

        m_mouseDownValid = False

        ' 拖动 (旋转视角) 不是点击：位移超过几个像素就忽略
        If System.Math.Abs(e.X - m_mouseDown.X) > 4 OrElse System.Math.Abs(e.Y - m_mouseDown.Y) > 4 Then
            Return
        End If

        Call pickAt(e.X, e.Y)
    End Sub

    ''' <summary>在画布的给定位置拾取神经元，并在详情面板里显示它的注释。</summary>
    Private Sub pickAt(x As Integer, y As Integer)
        If m_dataset Is Nothing OrElse m_scene Is Nothing Then Return

        Dim hit As SceneHitTest = m_canvas.HitTest(x, y, radius:=8)

        If Not hit.HasHit Then
            m_details.Text = "(此处没有神经元)" & Environment.NewLine & Environment.NewLine &
                             "提示: 左键拖动旋转，右键拖动平移，滚轮缩放；" & Environment.NewLine &
                             "点大小可以在工具条上调大，便于点中密集区域的神经元。"

            Return
        End If

        If hit.Kind = SceneHitKind.Point Then
            Dim neuron As Integer = m_scene.PointNeurons(hit.Index)

            m_focusNeuron = neuron

            ' 选中之后自动切到"该神经元的连接"模式：这是交互探索里最有用的动作
            m_buildOptions.Mode = ConnectionRenderMode.SelectedNeuron
            m_buildOptions.FocusNeuron = neuron
            m_connectionBox.SelectedIndex = 2
            m_showConnections.Checked = True

            Call showNeuronDetails(neuron)
            Call rebuildScene()
        Else
            Dim connection As Integer = If(m_scene.CandidateLines > 0, hit.Index, -1)
            Dim text As New StringBuilder()

            Call text.AppendLine($"连线 #{hit.Index}")
            Call text.AppendLine($"脑区   : {m_dataset.GetConnectionNeuropilName(hit.Index)}")
            Call text.AppendLine($"递质   : {m_dataset.GetConnectionNtTypeName(hit.Index)}")

            If hit.Index >= 0 AndAlso hit.Index < m_dataset.ConnectionCount Then
                Call text.AppendLine($"突触数 : {m_dataset.SynCount(hit.Index)}")
                Call text.AppendLine($"前神经元: {m_dataset.Index.GetRootId(m_dataset.Pre(hit.Index))}")
                Call text.AppendLine($"后神经元: {m_dataset.Index.GetRootId(m_dataset.Post(hit.Index))}")
            End If

            m_details.Text = text.ToString()
        End If
    End Sub

    ''' <summary>显示一个神经元的全部注释与连接统计。</summary>
    Private Sub showNeuronDetails(neuron As Integer)
        Dim index = m_dataset.Index
        Dim text As New StringBuilder()
        Dim position As FlywireAI.FAFBv783.NeuronPosition = m_dataset.GetPosition(neuron)
        Dim stats As (OutCount As Integer, OutSynapses As Long, InCount As Integer, InSynapses As Long) = countConnections(neuron)

        Call text.AppendLine($"neuron index     : {neuron}")
        Call text.AppendLine($"root_id          : {index.GetRootId(neuron)}")
        Call text.AppendLine($"position         : {position}")
        Call text.AppendLine($"marks            : {If(m_dataset.PositionMarks Is Nothing, 0, m_dataset.PositionMarks(neuron))} 个标记位置")

        If m_colorizer IsNot Nothing AndAlso Not m_colorizer.IsHeatMap Then
            Call text.AppendLine($"color category   : [{m_colorizer.Dimension}] {m_colorizer.GetCategoryName(neuron)}")
        End If

        Call text.AppendLine()
        Call text.AppendLine($"dominant neuropil: {m_dataset.GetNeuropilName(neuron)} " &
                             $"({m_dataset.NeuropilSynapses(neuron):N0} synapses)")
        Call text.AppendLine($"neurotransmitter : {m_dataset.Neurotransmitters(neuron)}")
        Call text.AppendLine($"excitatory       : {index.IsExcitatory(neuron)}")
        Call text.AppendLine()
        Call text.AppendLine($"annotated        : {index.IsAnnotated(neuron)}")
        Call text.AppendLine($"name             : {index.GetName(neuron)}")
        Call text.AppendLine($"group            : {index.GetGroup(neuron)}")
        Call text.AppendLine($"class            : {index.GetClass(neuron)}")
        Call text.AppendLine($"super class      : {index.GetSuperClass(neuron)}")
        Call text.AppendLine($"primary type     : {index.GetPrimaryType(neuron)}")
        Call text.AppendLine()

        If m_dataset.HasActivity Then
            Dim spikes As Double = m_dataset.Activity(neuron)

            Call text.AppendLine($"activity         : {spikes:N0} spikes ({spikes / System.Math.Max(1, m_config.TimeSteps):F3} /step)")
        End If

        Call text.AppendLine()
        Call text.AppendLine($"output           : {stats.OutCount} partners, {stats.OutSynapses:N0} synapses")
        Call text.AppendLine($"input            : {stats.InCount} partners, {stats.InSynapses:N0} synapses")

        m_details.Text = text.ToString()
    End Sub

    ''' <summary>
    ''' 统计一个神经元的出/入连接数与突触总数。
    ''' </summary>
    ''' <remarks>
    ''' 单次线性扫描 534 万条连接 (约 30 ms)，因此可以随点击实时计算，
    ''' 不需要预先为每个神经元建立邻接表 (那会多占用数百 MB 内存)。
    ''' </remarks>
    Private Function countConnections(neuron As Integer) As (OutCount As Integer, OutSynapses As Long, InCount As Integer, InSynapses As Long)
        Dim outCount As Integer = 0
        Dim inCount As Integer = 0
        Dim outSynapses As Long = 0
        Dim inSynapses As Long = 0

        If m_dataset Is Nothing Then Return (0, 0L, 0, 0L)

        For c As Integer = 0 To m_dataset.ConnectionCount - 1
            If m_dataset.Pre(c) = neuron Then
                outCount += 1
                outSynapses += m_dataset.SynCount(c)
            ElseIf m_dataset.Post(c) = neuron Then
                inCount += 1
                inSynapses += m_dataset.SynCount(c)
            End If
        Next

        Return (outCount, outSynapses, inCount, inSynapses)
    End Function

#End Region

End Class
