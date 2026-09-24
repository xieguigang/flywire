Imports System.IO
Imports System.Text
Imports System.Threading
Imports FlywireAI.FAFBv783
Imports Galaxy.Workbench
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports Microsoft.VisualBasic.Drawing.DirectX
Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D
Imports Microsoft.VisualBasic.Imaging
Imports Microsoft.VisualBasic.Imaging.Drawing3D
Imports Microsoft.VisualStudio.WinForms.Docking
Imports Neuropils.AppLogics
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
Public Class PageFlywireCanvas

    Friend ReadOnly m_renderer As New Direct3D11SceneRenderer()

    ''' <summary>
    ''' 状态栏链接的悬停提示。
    ''' </summary>
    Friend ReadOnly m_chartTip As New ToolTip
    Friend m_map As New FormNeuronMap

    Friend m_scene As BrainScene
    Friend m_lookup As Integer()
    Friend m_focusNeuron As Integer = -1
    Friend m_viewInitialized As Boolean = False
    ''' <summary>
    ''' 出图模式下要写入的图片路径 (``Nothing`` 表示交互模式)。
    ''' </summary>
    Friend m_snapshotPath As String = Nothing
    Friend m_cancel As CancellationTokenSource
    Friend m_busy As Boolean

    ''' <summary>
    ''' 拖动 / 筛选滑块的防抖定时器 (面板"应用"一次而不是每帧重建)。
    ''' </summary>
    ''' <remarks>
    ''' 必须写全 System.Windows.Forms.Timer：System.Threading 里也有一个同名的 Timer
    ''' (那个在多线程上触发回调，拿来更新界面会直接踩到跨线程访问)。
    ''' </remarks>
    Dim WithEvents m_rebuildTimer As New System.Windows.Forms.Timer()

#Region "life cycle"

    Public Sub openMap()
        If m_map Is Nothing Then
            m_map = New FormNeuronMap With {.Name = "neuron_map"}
        End If

        Call CommonRuntime.RegisterToolWindow(m_map, DockState.DockRight)
    End Sub

    Private Sub FormMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        m_experiment = New StimulationExperiment(Me)

        ' 状态栏里的"响应曲线 / 贪吃蛇"链接需要一条 Snake 实例与悬停提示，
        ' 这部分是运行时逻辑，保留在 initializeUi
        m_snake = New Snake
        ' m_chartTip.SetToolTip(m_chartLink, "")
        ' m_chartTip.SetToolTip(m_snakeLink, "")
        Call openMap()
        Call m_experiment.initializeStimulation()

        m_rebuildTimer.Stop()
        m_rebuildTimer.Interval = 260
        m_config.DataDir = DefaultDataDir

        ' 电刺激仿真默认尝试 GPU。注册失败会自动回退 CPU（GpuRuntime.TryRegister 的契约：
        ' 没有 NVIDIA 显卡 / NVRTC 缺失 / 驱动不匹配都返回 False，仿真照常跑完），
        ' 因此打开这个开关是安全的 —— 实际生效的后端会显示在状态栏与刺激报告里。
        m_config.UseGpu = True

        ' 回放需要"每一步哪些神经元发放"，它来自层内的逐步脉冲轨迹：
        ' 只有 KeepHistory = True 时 SHistory 才会有内容。
        ' 融合单步 + 双精度常驻则是让一次刺激保持在"点一下就能看到结果"的量级。
        m_config.KeepHistory = True
        m_config.UseFusedStep = True
        m_config.ResidentPrecision = LifResidentPrecision.Double64

        Dim args As String() = Environment.GetCommandLineArgs()

        ' msgpack 转储：把数据源的 csv 逐个转储成 msgpack 并打成一个 zip 包
        ' (``Neuropils.exe --dump <zip> [数据目录] [verify]``)
        If args.Length > 2 AndAlso String.Equals(args(1), "--dump", StringComparison.OrdinalIgnoreCase) Then
            Call AppLogics.MsgPackDump.runDump(args)

            Return
        End If

        ' 自检模式：把数据层与场景装配的实测结果写成报告，便于无人值守的回归
        If args.Length > 1 AndAlso String.Equals(args(1), "--selftest", StringComparison.OrdinalIgnoreCase) Then
            Call SelfTest.runSelfTest(Me, args)

            Return
        End If

        ' 电刺激探针：扫强度跑仿真并落盘回放数据（用于标定"按住时长 → 强度"的映射）
        If args.Length > 2 AndAlso String.Equals(args(1), "--stimulate", StringComparison.OrdinalIgnoreCase) Then
            Call m_experiment.runStimulationProbe(args)

            Return
        End If

        ' 响应曲线出图：把实验记录画成曲线图（与界面共用同一套装配与绘图代码）
        If args.Length > 2 AndAlso String.Equals(args(1), "--chart", StringComparison.OrdinalIgnoreCase) Then
            Call Chart.runChartExport(args)

            Return
        End If

        ' 响应曲线窗口自检：真的开窗、等首帧、抓帧写盘（验证"画在控件 GPU 画布上"这条路径）
        If args.Length > 2 AndAlso String.Equals(args(1), "--chart-window", StringComparison.OrdinalIgnoreCase) Then
            Call Chart.runChartWindowProbe(args)

            Return
        End If

        ' 果蝇大脑玩贪吃蛇：训练 + 三组对照评估（与观战窗口共用同一套代码）
        If args.Length > 2 AndAlso String.Equals(args(1), "--snake", StringComparison.OrdinalIgnoreCase) Then
            Call m_snake.runSnakeProbe(args)

            Return
        End If

        ' 观战窗口自检：正常载入数据 → 自动开窗 → 跑若干 tick → 抓屏退出
        If args.Length > 2 AndAlso String.Equals(args(1), "--snake-window", StringComparison.OrdinalIgnoreCase) Then
            m_snake.m_snakeProbePng = args(2)
            m_snake.m_snakeProbeTicks = If(args.Length > 4, CInt(Val(args(4))), 12)

            If m_snake.m_snakeProbeTicks <= 0 Then
                m_snake.m_snakeProbeTicks = 12
            End If
        End If

        ' 出图模式：载入 → 装配 → 抓一帧写成 png → 退出。
        ' 除了给论文/报告出图，它还是"渲染管线真的能跑起来"的可验证产物
        ' (自检模式只覆盖数据链路，不碰 GPU)。
        If args.Length > 2 AndAlso String.Equals(args(1), "--snapshot", StringComparison.OrdinalIgnoreCase) Then
            m_snapshotPath = args(2)
            m_config.DataDir = If(args.Length > 3, args(3), m_config.DataDir)

            ' 参数位: <png> [dataDir] [着色维度 0..4] [连接模式 0=不画 1=逐条 2=宏连接 3=选中神经元]
            If args.Length > 4 Then
                m_dimensionBox.SelectedIndex = System.Math.Max(0, System.Math.Min(4, CInt(Val(args(4)))))
            End If

            If args.Length > 5 Then
                Dim connections As Integer = CInt(Val(args(5)))

                If connections > 0 Then
                    m_connectionBox.SelectedIndex = System.Math.Max(0, System.Math.Min(2, connections - 1))
                    RibbonMenu.ToggleConnection = True
                End If
            End If

            ' 可选：先跑一次电刺激再把指定步的高亮画出来（离线验证回放渲染）
            If args.Length > 6 AndAlso args(6).StartsWith("stim=", StringComparison.OrdinalIgnoreCase) Then
                Call m_experiment.parseSnapshotStimulus(args(6).Substring("stim=".Length))
            End If
        ElseIf args.Length > 1 AndAlso Directory.Exists(args(1)) Then
            m_config.DataDir = args(1)
        End If

        Call ApplyVsTheme(m_toolStrip)
        Call startLoad()
    End Sub

    ''' <summary>抓一帧写成图片，然后结束进程 (出图模式)。</summary>
    Friend Sub captureAndExit()
        Dim file As String = m_snapshotPath

        m_snapshotPath = Nothing

        Call BeginInvoke(
            Sub()
                Try
                    ' 抓帧内部会强制一次同步重绘，因此此刻的布局与首帧渲染都已经完成
                    If m_canvas.SaveSnapshot(file, ImageFormats.Png) Then
                        Call Console.Out.WriteLine($"snapshot saved: {file} ({m_scene})")
                    Else
                        Call Console.Out.WriteLine($"snapshot failed: {m_canvas.LastError}")
                    End If
                Catch ex As Exception
                    Call Console.Out.WriteLine($"snapshot failed: {ex.GetType().Name}: {ex.Message}")
                End Try

                Call Console.Out.Flush()
                Call Environment.Exit(0)
            End Sub)
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        m_rebuildTimer.Stop()
        m_experiment.m_replayTimer.Stop()
        m_experiment.m_holdTimer.Stop()

        If m_cancel IsNot Nothing Then
            Call m_cancel.Cancel()
        End If

        ' 让刺激工作线程退出循环（后台线程本来就不会阻止进程结束，这里只是把收尾做干净）
        If m_experiment.m_stimWork IsNot Nothing AndAlso Not m_experiment.m_stimWork.IsAddingCompleted Then
            Call m_experiment.m_stimWork.CompleteAdding()
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
        CommonRuntime.StatusMessage($"正在载入 {m_config.DataDir} ...")
        progress.Visible = True
        m_dataset = Nothing
        m_viewInitialized = False
        Call m_canvas.ClearScene()

        Dim loader As New BrainDatasetLoader(m_config)

        ' 进度回调来自后台线程，切回 UI 线程再更新状态栏
        Task.Run(Function() loader.Load(AddressOf onLoadProgress, tokenSource.Token)) _
            .ContinueWith(AddressOf onLoadCompleted, TaskScheduler.FromCurrentSynchronizationContext())
    End Sub

    Friend Sub onLoadProgress(message As String)
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return

        Call BeginInvoke(New Action(
            Sub()
                CommonRuntime.StatusMessage(message)
            End Sub))
    End Sub

    Private Sub onLoadCompleted(task As Task(Of BrainDataset))
        m_busy = False
        progress.Visible = False

        If task.IsCanceled Then
            CommonRuntime.StatusMessage("载入已取消")
            Return
        End If

        If task.IsFaulted Then
            Dim reason As String = If(task.Exception?.GetBaseException()?.Message, "unknown error")

            CommonRuntime.Warning("载入失败")
            Call MessageBox.Show(Me, reason, "载入数据失败", MessageBoxButtons.OK, MessageBoxIcon.Error)

            Return
        End If

        m_dataset = task.Result
        CommonRuntime.Success($"数据载入完成: {m_dataset}")
        Workbench.SceneText(m_dataset.ToString)

        If m_dataset.Units = 0 Then
            Return
        End If

        ' 着色维度以工具条上的当前选择为准（默认是主导脑区）。
        ' 这里不能写死 Neuropil：--snapshot 模式会在载入之前就把维度设好，
        ' 写死会让"着色维度"这个命令行参数完全失效（出图出来的还是默认维度）。
        Call rebuildColorizer(dimensionFromUi(), resetUi:=False)
        Call rebuildScene(resetView:=True)

        ' 有历史实验记录时就可以直接看响应曲线（不必先做一次刺激）
        If Analysis.StimulationArchive.Latest(m_config.ResolveActivityDir()) IsNot Nothing Then
            ribbon.ButtonOpenResponseCharts.Enabled = True
        End If

        ' 观战窗口自检：数据就绪后自动开窗
        If m_snake.m_snakeProbePng IsNot Nothing Then
            Call m_snake.openSnakeWindow()
        End If
    End Sub

    ''' <summary>
    ''' 工具条上当前选中的着色维度（界面上唯一一处"维度 → 枚举"的映射）。
    ''' </summary>
    Private Function dimensionFromUi() As NeuronColorDimension
        Select Case m_dimensionBox.SelectedIndex
            Case 1
                Return NeuronColorDimension.Neurotransmitter
            Case 2
                Return NeuronColorDimension.CellType
            Case 3
                Return NeuronColorDimension.SuperClass
            Case 4
                Return NeuronColorDimension.Activity
            Case Else
                Return NeuronColorDimension.Neuropil
        End Select
    End Function

    ''' <summary>
    ''' 手工选中 msgpack 转储包并重新载入模型数据。
    ''' </summary>
    ''' <remarks>
    ''' 启动时按 <see cref="FafbMsgPackStorage.CandidatePackPaths"/> 的顺序自动找包
    ''' （穷尽之后不会再自动尝试）；一个都没找到时，用户唯一的选择就是这里的
    ''' 文件对话框 —— 选中 <c>fafb-v783.msgpack.zip</c> 之后写进
    ''' <see cref="SnnConfig.PackFile"/>，之后主界面与贪吃蛇等所有数据消费方都用这一份。
    ''' 
    ''' 数据目录 (csv 所在目录) 不在这里改：它只影响"新鲜度指纹核对"（源 csv 不在就跳过）
    ''' 与活动结果文件的搜索，与转储包在哪里无关。
    ''' </remarks>
    Public Sub onReload()
        If m_busy Then Return

        Using dialog As New OpenFileDialog()
            dialog.Title = "选择 msgpack 模型数据包 (fafb-v783.msgpack.zip)"
            dialog.Filter = "msgpack 模型数据包 (*.zip)|*.zip|全部文件 (*.*)|*.*"
            dialog.FileName = FafbMsgPackStorage.DefaultPackName
            dialog.CheckFileExists = True

            ' 默认落在上一次用过的位置；第一次就落在自动查找的第一个候选目录
            Dim initial As String = FafbMsgPackStorage.CandidatePackPaths().FirstOrDefault

            If Not String.IsNullOrWhiteSpace(initial) Then
                dialog.InitialDirectory = IO.Path.GetDirectoryName(initial)
            End If

            If dialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            m_config.PackFile = dialog.FileName
        End Using

        Call startLoad()
    End Sub

#End Region

#Region "scene assembly"

    ''' <summary>重建着色器与图例。</summary>
    Friend Sub rebuildColorizer(dimension As NeuronColorDimension, resetUi As Boolean)
        If m_dataset Is Nothing Then Return

        m_colorizer = NeuronColorizer.Create(m_dataset, dimension)

        If m_colorizer Is Nothing Then Return

        If m_colorizer.IsHeatMap Then
            ' 热力图用渲染管线的调色板纹理，点云改走 Intensity 通道
            m_canvas.UseEmbeddedColor = False
            m_canvas.ColorScheme = "viridis"
            Call m_map.showGradient()
        Else
            m_canvas.UseEmbeddedColor = True
            m_map.m_gradient.Visible = False
        End If

        If resetUi Then
            Call m_map.refreshLegend()
        End If
    End Sub

    ''' <summary>重建点云与连线并送进画布。</summary>
    Friend Sub rebuildScene(Optional resetView As Boolean = False)
        If m_dataset Is Nothing OrElse m_colorizer Is Nothing Then Return
        If m_busy Then Return

        m_busy = True
        CommonRuntime.StatusMessage("正在装配场景 ...")
        progress.Visible = True

        If m_buildOptions.Mode = ConnectionRenderMode.SelectedNeuron Then
            ' 还没点过任何神经元时，先用"最忙的神经元"作为默认目标，
            ' 否则这个模式一开始会是空的 (看起来像功能失效)
            If m_focusNeuron < 0 Then
                m_focusNeuron = BrainSceneBuilder.FindBusiestNeuron(m_dataset)

                If m_focusNeuron >= 0 Then
                    Call showNeuronDetails(m_focusNeuron)
                End If
            End If

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
                    progress.Visible = False

                    If task.IsFaulted Then
                        CommonRuntime.Warning($"场景装配失败: {If(task.Exception?.GetBaseException()?.Message, "unknown")}")

                        Return
                    End If

                    timer.Stop()

                    m_scene = task.Result
                    m_lookup = m_scene.BuildLookup(dataset.Units)

                    Call applyScene(resetView)

                    CommonRuntime.Success($"场景就绪 ({timer.ElapsedMilliseconds} ms): {m_scene.Describe()}")
                    Workbench.SceneText(m_scene.Describe)
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

            If RibbonMenu.ToggleConnection Then
                Call m_canvas.UpdateConnections(m_scene.Lines)
            End If
        End If

        ' 选中标记：在选中神经元周围画一个小十字，任何缩放下都看得见
        If m_focusNeuron >= 0 AndAlso m_focusNeuron < m_dataset.Units AndAlso m_dataset.HasPosition(m_focusNeuron) Then
            Call drawFocusMarker(m_focusNeuron)
        End If

        ' 场景换了（着色维度 / 筛选 / 连接档位变化都会换）：回放高亮器要重新绑定，
        ' 否则它会把旧配色下的颜色写进新点云
        Call m_experiment.rebindHighlighter()

        ' 出图模式若带刺激规格：先跑仿真并高亮，抓帧交给它自己完成
        If m_experiment.m_snapshotStimulus.HasValue Then
            Call m_experiment.applySnapshotStimulus()

            Return
        End If

        If m_snapshotPath IsNot Nothing Then
            Call captureAndExit()
        End If
    End Sub

    ''' <summary>在选中神经元周围补三根短十字线 (与连线一起提交)。</summary>
    Private Sub drawFocusMarker(neuron As Integer)
        Dim position As FlywireAI.FAFBv783.NeuronPosition = m_dataset.GetPosition(neuron)
        Dim lines As New List(Of LineSegment)(3)
        Dim size As Double = 6000.0     ' 纳米：约 6 微米，全脑尺度下可见又不夸张
        Dim color As Color = Color.FromArgb(255, 255, 255)

        For axis As Integer = 0 To 2
            Dim a As New Point3D(position.X, position.Y, position.Z)
            Dim b As New Point3D(position.X, position.Y, position.Z)

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
        RibbonMenu.ToggleConnection = True
    End Sub

    Private Sub onRebuildTimerTick(sender As Object, e As EventArgs) Handles m_rebuildTimer.Tick
        m_rebuildTimer.Stop()
        Call rebuildScene()
    End Sub

    ''' <summary>请求一次防抖之后的重建 (滑块 / 勾选频繁触发时使用)。</summary>
    Public Sub scheduleRebuild()
        m_rebuildTimer.Stop()
        m_rebuildTimer.Start()
    End Sub

#End Region

#Region "toolbar events"

    Private Sub onDimensionChanged(sender As Object, e As EventArgs) Handles m_dimensionBox.SelectedIndexChanged
        If m_dataset Is Nothing Then Return

        Dim dimension As NeuronColorDimension = dimensionFromUi()

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

    Private Sub onRenderModeChanged(sender As Object, e As EventArgs) Handles m_renderModeBox.SelectedIndexChanged
        Select Case m_renderModeBox.SelectedIndex
            Case 1 : m_canvas.RenderMode = SceneRenderMode.Mesh
            Case 2 : m_canvas.RenderMode = SceneRenderMode.Surface
            Case Else : m_canvas.RenderMode = SceneRenderMode.PointCloud
        End Select
    End Sub

    Private Sub onConnectionModeChanged(sender As Object, e As EventArgs) Handles m_connectionBox.SelectedIndexChanged
        Select Case m_connectionBox.SelectedIndex
            Case 1 : m_buildOptions.Mode = ConnectionRenderMode.NeuropilAggregate
            Case 2 : m_buildOptions.Mode = ConnectionRenderMode.SelectedNeuron
            Case Else : m_buildOptions.Mode = ConnectionRenderMode.PerConnection
        End Select

        Call scheduleRebuild()
    End Sub

    Private Sub onLineColorChanged(sender As Object, e As EventArgs) Handles m_lineColorBox.SelectedIndexChanged
        Select Case m_lineColorBox.SelectedIndex
            Case 1 : m_buildOptions.LineColor = LineColorMode.Neurotransmitter
            Case 2 : m_buildOptions.LineColor = LineColorMode.Uniform
            Case Else : m_buildOptions.LineColor = LineColorMode.PresynapticNeuron
        End Select

        Call scheduleRebuild()
    End Sub

    Public Sub onSnapshot()
        If Not m_viewInitialized Then Return

        Using dialog As New SaveFileDialog()
            dialog.Filter = "PNG 图像|*.png"
            dialog.FileName = $"drosophila_brain_{DateTime.Now:yyyyMMdd_HHmmss}.png"

            If dialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            If m_canvas.SaveSnapshot(dialog.FileName, ImageFormats.Png) Then
                CommonRuntime.StatusMessage($"截图已保存: {dialog.FileName}")
            Else
                CommonRuntime.Warning($"截图保存失败: {m_canvas.LastError}")
            End If
        End Using
    End Sub

#End Region

#Region "picking"

    Private m_mouseDown As Point
    Private m_mouseDownValid As Boolean

    Private Sub onCanvasMouseDown(sender As Object, e As MouseEventArgs) Handles m_canvas.MouseDown
        If e.Button <> MouseButtons.Left Then Return

        m_mouseDown = New Point(e.X, e.Y)
        m_mouseDownValid = True

        ' 电刺激模式下同时开始"按住计时"：强度由按住时长决定，因此在按下期间就要回显
        If RibbonMenu.ToggleSimulationExperiment Then
            Call m_experiment.beginStimulationHold(e.X, e.Y)
        End If
    End Sub

    Private Sub onCanvasMouseUp(sender As Object, e As MouseEventArgs) Handles m_canvas.MouseUp
        If e.Button <> MouseButtons.Left OrElse Not m_mouseDownValid Then Return

        m_mouseDownValid = False

        ' 先取按住时长再判断是不是点击：无论走哪条分支都要把计时停下来
        Dim holdMs As Long = m_experiment.endStimulationHold()

        ' 拖动 (旋转视角) 不是点击：位移超过几个像素就忽略
        If System.Math.Abs(e.X - m_mouseDown.X) > 4 OrElse System.Math.Abs(e.Y - m_mouseDown.Y) > 4 Then
            Call m_experiment.cancelStimulationHold()

            Return
        End If

        If RibbonMenu.ToggleSimulationExperiment Then
            Call m_experiment.stimulateAt(e.X, e.Y, holdMs)
        Else
            Call m_experiment.cancelStimulationHold()
            Call pickAt(e.X, e.Y)
        End If
    End Sub

    ''' <summary>在画布的给定位置拾取神经元，并在详情面板里显示它的注释。</summary>
    Private Sub pickAt(x As Integer, y As Integer)
        If m_dataset Is Nothing OrElse m_scene Is Nothing Then Return

        Dim hit As SceneHitTest = m_canvas.HitTest(x, y, radius:=8)

        If Not hit.HasHit Then
            m_map.m_details.Text = "(此处没有神经元)" & Environment.NewLine & Environment.NewLine &
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
            RibbonMenu.ToggleConnection = True

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

            m_map.m_details.Text = text.ToString()
        End If
    End Sub

    ''' <summary>
    ''' 当前点云是否由调色板着色（即"仿真活跃度"热力图维度）。
    ''' </summary>
    ''' <remarks>
    ''' 决定回放高亮要改哪个字段：热力图模式下着色器不看嵌入色，只能抬热值 +
    ''' 放大尺寸（尺寸在两种模式下都能用，见 <c>PointCloudPoint.SizeScale</c>）。
    ''' </remarks>
    Public Function isHeatMap() As Boolean
        Return m_colorizer IsNot Nothing AndAlso m_colorizer.IsHeatMap
    End Function

    ''' <summary>显示一个神经元的全部注释与连接统计。</summary>
    Friend Sub showNeuronDetails(neuron As Integer)
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

        m_map.m_details.Text = text.ToString()
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

    Private Sub PageFlywireCanvas_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        e.Cancel = True
        DockState = DockState.Hidden
    End Sub

#End Region

End Class
