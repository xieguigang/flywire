Imports System.Collections.Concurrent
Imports System.IO
Imports System.Text
Imports System.Threading
Imports Galaxy.Workbench
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D
Imports Neuropils.Data
Imports Neuropils.Rendering
Imports Neuropils.Simulation

Namespace AppLogics

    ' 电刺激仿真模式：按住左键点神经元 → 按时长决定强度 → 跑一次全脑 SNN → 回放激活过程。
    '
    ' 交互链路：
    ' 
    ' 1. <b>按住</b>：记下按下位置与计时器，每 60 ms 刷新一次"目标神经元 + 当前强度"，
    '    让用户在松手之前就知道这一下会有多强；
    ' 2. <b>松开</b>：位移小于 4 px 才算"点击"（否则是在旋转视角）。点击时把按住时长映射成
    '    注入电流强度，连同目标神经元一起交给后台任务；
    ' 3. <b>后台</b>：首次需要装配连接组（读 261 MB 连接表 + 标定增益），之后每次点击
    '    只是"装配一个单输入网络 + 跑 T 步"；结果落到 ``snn-output\&lt;时间戳&gt;_stimulation\``；
    ' 4. <b>回放</b>：定时器逐步推进，把每一步被激活的神经元<b>点亮并放大</b>，
    '    前几步的神经元按权重留下余辉，形成"一波激活扩散出去"的观感。

    Public Class StimulationExperiment

#Region "constants"

        ''' <summary>低于这个按住时长就按最弱刺激处理。</summary>
        Private Const HoldFloorMs As Long = 120

        ''' <summary>按住到这个时长即达到映射区间的上界。</summary>
        Private Const HoldCeilingMs As Long = 1400

        ''' <summary>募集半径的下界（纳米）：约 25 微米，电极尖端的最近一圈。</summary>
        Private Const RadiusFloorNm As Double = 25000

        ''' <summary>募集半径的上界（纳米）：约 140 微米，一个局部脑区尺度的微刺激。</summary>
        Private Const RadiusCeilingNm As Double = 140000

        ''' <summary>注入电流的下界（略高于阈值 1.0，保证被募集的神经元会发放）。</summary>
        Private Const CurrentFloor As Double = 1.2

        ''' <summary>注入电流的上界。</summary>
        Private Const CurrentCeiling As Double = 2.5

        ''' <summary>一次"按住"映射出来的刺激参数。</summary>
        Private Structure HoldStimulus
            ''' <summary>募集半径（纳米）。</summary>
            Public RadiusNm As Double

            ''' <summary>注入电流强度。</summary>
            Public Current As Double
        End Structure

        ''' <summary>回放时的余辉长度（之前多少步仍然可见）。</summary>
        Private Const ReplayTrail As Integer = 3

#End Region

#Region "fields"

        ''' <summary>按住计时（鼠标左键）。</summary>
        Private ReadOnly m_holdWatch As New Stopwatch()

        ''' <summary>按住过程中的强度回显定时器。</summary>
        Friend m_holdTimer As System.Windows.Forms.Timer

        ''' <summary>回放时钟。</summary>
        Friend ReadOnly m_replayTimer As New System.Windows.Forms.Timer()

        ''' <summary>按住期间命中的神经元（-1 表示还没命中）。</summary>
        Private m_holdNeuron As Integer = -1

        ''' <summary>刺激引擎（首次使用时装配）。</summary>
        Private m_stimulator As BrainStimulator

        ''' <summary>是否正在跑刺激仿真。</summary>
        Private m_stimBusy As Boolean

        ''' <summary>刺激仿真专用的工作线程与其任务队列。</summary>
        Friend m_stimWorker As Thread
        Friend m_stimWork As BlockingCollection(Of Action)

        ''' <summary>最近一次刺激的结果。</summary>
        Friend m_replay As StimulationReplay

        ''' <summary>把回放状态映射到点云颜色/尺寸的高亮器。</summary>
        Private m_highlighter As ReplayHighlighter

        ''' <summary>当前回放到的步号（0 基，-1 表示没有回放）。</summary>
        Private m_replayStep As Integer = -1

        ''' <summary>回放是否在自动播放。</summary>
        Private m_replayPlaying As Boolean

#End Region

        ReadOnly main As PageFlywireCanvas

        Sub New(main As PageFlywireCanvas)
            Me.main = main
        End Sub

#Region "setup"

        ''' <summary>
        ''' 初始化电刺激/回放相关的定时器与面板（由 <c>initializeUi</c> 在控件建好之后调用）。
        ''' </summary>
        Friend Sub initializeStimulation()
            m_holdTimer = New System.Windows.Forms.Timer With {.Interval = 60}
            AddHandler m_holdTimer.Tick, AddressOf onHoldTimerTick

            m_replayTimer.Interval = 120
            AddHandler m_replayTimer.Tick, AddressOf onReplayTimerTick

            Call updateReplayPanel()
        End Sub

        ''' <summary>按住时长 → 刺激参数（募集半径 + 注入电流）。</summary>
        ''' <remarks>
        ''' <b>为什么"按住越久"映射成"募集的神经元越多"</b>
        ''' 
        ''' 初版按"电流强度"映射，实测发现：本数据集的权重经过结构归一化，单个神经元即使
        ''' 持续注入 16 倍阈值电流也只能激活它自己（全脑 139,255 神经元 / 373 万突触下实测
        ''' active = 1，零传播）。这是网络动力学的正确结果 —— 单个突触前神经元对突触后的
        ''' 贡献只有 1/扇入 量级，靠"一个神经元"无论如何也点不着网络。
        ''' 
        ''' 真实电刺激也是如此：电流从电极尖端扩散，兴奋的是<b>一片</b>神经元，
        ''' 微刺激实验里"强度"指的就是被募集的组织范围。因此这里把按住时长映射成
        ''' 「募集半径（25 → 140 微米）」与「注入电流（1.2 → 2.5）」，
        ''' 工具条上的"强度×"再整体缩放半径。
        ''' </remarks>
        Private Function stimulusFromHold(holdMs As Long) As HoldStimulus
            Dim t As Double = (holdMs - HoldFloorMs) / CDbl(HoldCeilingMs - HoldFloorMs)

            If t < 0 Then t = 0
            If t > 1 Then t = 1

            Dim scale As Double = RibbonMenu.SimulationIntensity

            If scale <= 0 Then scale = 1

            Return New HoldStimulus With {
            .RadiusNm = (RadiusFloorNm + t * (RadiusCeilingNm - RadiusFloorNm)) * scale,
            .Current = CurrentFloor + t * (CurrentCeiling - CurrentFloor)
        }
        End Function

#End Region

#Region "hold interaction"

        Friend Sub onStimulateModeChanged(sender As Object, e As EventArgs)
            If RibbonMenu.ToggleSimulationExperiment Then
                CommonRuntime.StatusMessage("电刺激模式：在神经元上按住左键（越久越强），松开后自动运行全脑仿真并回放")
                main.m_holdLabel.Text = ""

                ' 回放时先收掉连线：几十万根半透明线会把点亮的神经元淹掉
                RibbonMenu.ToggleConnection = False
            Else
                Call cancelStimulationHold()
                Call stopReplay(clearHighlight:=True)

                CommonRuntime.StatusMessage("就绪")
            End If
        End Sub

        ''' <summary>鼠标按下：开始计时并进入按住预览。</summary>
        Friend Sub beginStimulationHold(x As Integer, y As Integer)
            If m_dataset Is Nothing OrElse main.m_scene Is Nothing Then Return
            If m_stimBusy Then Return

            m_holdNeuron = -1
            m_holdWatch.Restart()
            m_holdTimer.Start()

            Call updateHoldPreview(x, y)
        End Sub

        Private Sub onHoldTimerTick(sender As Object, e As EventArgs)
            If Not m_holdWatch.IsRunning Then
                m_holdTimer.Stop()
                Return
            End If

            Dim position As Point = main.m_canvas.PointToClient(Cursor.Position)

            Call updateHoldPreview(position.X, position.Y)
        End Sub

        ''' <summary>刷新"目标 + 当前刺激参数"的回显。</summary>
        Private Sub updateHoldPreview(x As Integer, y As Integer)
            Dim holdMs As Long = m_holdWatch.ElapsedMilliseconds
            Dim stimulus As HoldStimulus = stimulusFromHold(holdMs)
            Dim target As String = ""

            ' 每次预览都做一次拾取：13 万点的拾取约 6 ms，60 ms 一次完全够用
            Dim hit As SceneHitTest = main.m_canvas.HitTest(x, y, radius:=8)

            If hit.HasHit AndAlso hit.Kind = SceneHitKind.Point Then
                m_holdNeuron = main.m_scene.PointNeurons(hit.Index)

                Dim rootId As Long = m_dataset.Index.GetRootId(m_holdNeuron)

                ' 顺便实测一次"这一下会募集多少个神经元"：一次线性扫描约 1 ms
                Dim recruited As Integer() = BrainStimulator.Recruit(m_dataset, m_holdNeuron, stimulus.RadiusNm)

                target = $" · 目标 #{m_holdNeuron} ({m_dataset.GetNeuropilName(m_holdNeuron)}, root_id {rootId})" &
                     $" · 募集 {recruited.Length:N0} 个"
            Else
                m_holdNeuron = -1
                target = " · (未命中神经元)"
            End If

            main.m_holdLabel.Text = $"按住 {holdMs / 1000.0:F1}s → 半径 {stimulus.RadiusNm / 1000.0:F0}μm / " &
                           $"电流 {stimulus.Current:F1}{target}"
        End Sub

        ''' <summary>结束按住计时，返回按住时长（毫秒）。</summary>
        Friend Function endStimulationHold() As Long
            Dim elapsed As Long = m_holdWatch.ElapsedMilliseconds

            m_holdWatch.Stop()
            m_holdTimer.Stop()

            Return elapsed
        End Function

        Friend Sub cancelStimulationHold()
            m_holdWatch.Reset()
            m_holdTimer.Stop()
            m_holdNeuron = -1
            main.m_holdLabel.Text = ""
        End Sub

#End Region

#Region "stimulation"

        ''' <summary>在指定位置施加电刺激（点击命中神经元时）。</summary>
        Friend Sub stimulateAt(x As Integer, y As Integer, holdMs As Long)
            If m_dataset Is Nothing OrElse main.m_scene Is Nothing Then Return

            If m_stimBusy Then
                CommonRuntime.StatusMessage("上一次电刺激还在运行，请稍等")

                Return
            End If

            Dim hit As SceneHitTest = main.m_canvas.HitTest(x, y, radius:=8)

            If Not hit.HasHit OrElse hit.Kind <> SceneHitKind.Point Then
                CommonRuntime.StatusMessage("电刺激：这里没有神经元（把点大小调大一点更容易点中）")

                Return
            End If

            Dim neuron As Integer = main.m_scene.PointNeurons(hit.Index)
            Dim stimulus As HoldStimulus = stimulusFromHold(holdMs)

            main.m_focusNeuron = neuron

            Call main.showNeuronDetails(neuron)
            Call startStimulation(neuron, stimulus, holdMs)
        End Sub

        ''' <summary>在后台装配（首次）并运行一次刺激仿真，完成后自动进入回放。</summary>
        Private Sub startStimulation(neuron As Integer, stimulus As HoldStimulus, holdMs As Long)
            If m_stimulator Is Nothing Then
                m_stimulator = New BrainStimulator(m_config, m_dataset)
            End If

            Call stopReplay(clearHighlight:=True)
            Call cancelStimulationHold()

            RibbonMenu.ToggleSimulationExperiment = True

            Dim stimulator As BrainStimulator = m_stimulator
            Dim index As Integer = neuron
            Dim radius As Double = stimulus.RadiusNm
            Dim current As Double = stimulus.Current

            m_stimBusy = True
            progress.Visible = True
            progress.Style = ProgressBarStyle.Marquee
            CommonRuntime.StatusMessage($"电刺激 #{neuron}：半径 {radius / 1000.0:F0}μm / 电流 {current:F1}，正在运行全脑仿真 ...")

            Call setStimulusMarker(neuron)
            Call ensureStimWorker()

            ' 整段（装配连接组 + 仿真 + 落盘）都提交到专用线程执行，见 ensureStimWorker 的说明
            Call m_stimWork.Add(
            Sub()
                Try
                    Dim prepared As Boolean = stimulator.Prepare(AddressOf main.onLoadProgress, CancellationToken.None)

                    If Not prepared Then
                        Throw New InvalidOperationException($"刺激引擎装配失败: {stimulator.LastError}")
                    End If

                    Dim result As StimulationReplay = stimulator.Stimulate(index, radius, current, holdMs, AddressOf main.onLoadProgress)

                    ' 记录仿真结果（逐步激活清单 + 统计 + 活跃度快照）
                    Call StimulationReport.Write(result, m_config.ResolveActivityDir(), m_dataset.Index)

                    Call main.BeginInvoke(New Action(Sub() onStimulationCompleted(result)))
                Catch ex As Exception
                    Dim reason As String = $"{ex.GetType().Name}: {ex.Message}"

                    Call main.BeginInvoke(New Action(Sub() onStimulationFailed(reason)))
                End Try
            End Sub)
        End Sub

        ''' <summary>
        ''' 刺激仿真专用的工作线程。
        ''' </summary>
        ''' <remarks>
        ''' <b>为什么不用线程池</b>：CUDA 的"当前上下文"是<b>线程局部</b>状态，上下文在创建它的
        ''' 那条线程上绑定。把整个刺激流程固定在同一条线程上执行，上下文就始终"属于"这条线程：
        ''' 
        ''' * 不会再出现"第一次刺激正常、第二次点击报
        '''   <c>cuMemAlloc_v2 failed: CUDA_ERROR_INVALID_CONTEXT (201)</c>"——
        '''   那是线程池把第二次任务调度到另一条线程上的结果
        '''   （ILCuda 侧也已修好：<c>CudaRuntime.EnsureCurrent</c> 会在每个底层调用前自动为当前线程
        '''   绑定上下文，这里是双保险）；
        ''' * 同一时刻只有一次仿真在碰 GPU（配合 <c>m_stimBusy</c> 的界面侧互斥）。
        ''' </remarks>
        Private Sub ensureStimWorker()
            If m_stimWorker IsNot Nothing Then Return

            m_stimWork = New BlockingCollection(Of Action)()
            m_stimWorker = New Thread(AddressOf stimWorkerLoop) With {
            .IsBackground = True,
            .Name = "SNN stimulation"
        }
            m_stimWorker.Start()
        End Sub

        Private Sub stimWorkerLoop()
            For Each work As Action In m_stimWork.GetConsumingEnumerable()
                Try
                    Call work()
                Catch ex As Exception
                    ' 单个任务失败不能让工作线程退出：否则后面每一次点击都会失败
                    System.Diagnostics.Debug.WriteLine($"stimulation worker: {ex.Message}")
                End Try
            Next
        End Sub

        Private Sub onStimulationCompleted(replay As StimulationReplay)
            m_stimBusy = False
            progress.Visible = False
            progress.Style = ProgressBarStyle.Continuous

            m_replay = replay

            ' 把这次刺激的逐神经元计数接进"仿真活跃度"着色：点一次就能直接看全脑的热力分布
            If m_dataset IsNot Nothing AndAlso replay.Counts IsNot Nothing AndAlso replay.Counts.Length = m_dataset.Units Then
                m_dataset.Activity = replay.Counts
                m_dataset.ActivitySource = $"电刺激 #{replay.Neuron} (强度 {replay.Strength:F2})"

                If main.m_colorizer IsNot Nothing AndAlso main.m_colorizer.Dimension = NeuronColorDimension.Activity Then
                    Call main.rebuildColorizer(NeuronColorDimension.Activity, resetUi:=True)
                    Call main.rebuildScene()
                End If
            End If

            m_replayStep = 0
            m_highlighter = New ReplayHighlighter(main.m_scene, m_dataset.Units, main.isHeatMap())

            Call updateReplayPanel()
            Call updateReplayFrame()

            m_replayPlaying = True
            m_replayTimer.Start()

            CommonRuntime.Success($"电刺激完成 ({replay.WallMs} ms, {replay.Backend}/{replay.StepPath}): {replay.Describe()}")

            If Not String.IsNullOrEmpty(replay.ReportDir) Then
                Workbench.SceneText($"结果已记录: {replay.ReportDir}")
            End If

            ' 刺激完成 → 右下角的"响应曲线"链接可用
            ribbon.ButtonOpenResponseCharts.Enabled = True
        End Sub

        ''' <summary>刺激失败：把原因如实报出来（界面回到可用状态，下一次点击仍然可以重试）。</summary>
        Private Sub onStimulationFailed(reason As String)
            m_stimBusy = False
            progress.Visible = False
            progress.Style = ProgressBarStyle.Continuous
            CommonRuntime.Warning("电刺激失败")

            Call MessageBox.Show(Me, reason, "电刺激仿真失败", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Sub

#End Region

#Region "replay"

        Private Sub onReplayTimerTick(sender As Object, e As EventArgs)
            If Not m_replayPlaying OrElse m_replay Is Nothing Then Return

            If m_replayStep + 1 >= m_replay.Steps Then
                ' 播到最后一帧就停下并保持画面（继续按播放会从头再来）
                m_replayPlaying = False
                m_replayTimer.Stop()
                Call updateReplayPanel()

                Return
            End If

            m_replayStep += 1
            Call updateReplayFrame()
        End Sub

        ''' <summary>把当前回放帧刷到点云上。</summary>
        Private Sub updateReplayFrame()
            If m_replay Is Nothing OrElse m_highlighter Is Nothing Then Return
            If m_replayStep < 0 Then m_replayStep = 0
            If m_replayStep >= m_replay.Steps Then m_replayStep = m_replay.Steps - 1

            Call m_highlighter.Apply(m_replay, m_replayStep, ReplayTrail)
            Call main.m_canvas.UpdatePointCloud(m_highlighter.Points)

            Dim cumulative As Double = 0

            For t As Integer = 0 To m_replayStep
                If m_replay.SpikesPerStep IsNot Nothing AndAlso t < m_replay.SpikesPerStep.Length Then
                    cumulative += m_replay.SpikesPerStep(t)
                End If
            Next

            Dim active As Integer = If(m_replay.ActivePerStep IsNot Nothing AndAlso m_replayStep < m_replay.ActivePerStep.Length,
                                   m_replay.ActivePerStep(m_replayStep), 0)

            main.m_replayText.Text = $"步 {m_replayStep + 1}/{m_replay.Steps} · 活跃 {active:N0} · 累积 {cumulative:N0} / 总 {m_replay.TotalSpikes:N0}"

            Call updateReplayPanel()
        End Sub

        ''' <summary>刷新回放面板的可用状态与进度条位置。</summary>
        Private Sub updateReplayPanel()
            Dim hasReplay As Boolean = m_replay IsNot Nothing

            main.m_replayTrack.Enabled = hasReplay
            main.m_replayFirst.Enabled = hasReplay
            main.m_replayPrev.Enabled = hasReplay
            main.m_replayNext.Enabled = hasReplay
            main.m_replayLast.Enabled = hasReplay
            main.m_replayPlay.Enabled = hasReplay
            main.m_replayClear.Enabled = hasReplay
            main.m_replaySpeed.Enabled = hasReplay

            If Not hasReplay Then
                main.m_replayText.Text = If(RibbonMenu.ToggleSimulationExperiment,
                                   "在神经元上按住左键并松开即可施加电刺激",
                                   "勾选工具条上的「电刺激模式」后可用")
                main.m_replayTrack.Maximum = 0

                Return
            End If

            main.m_replayTrack.Maximum = System.Math.Max(1, m_replay.Steps - 1)

            If m_replayStep >= 0 Then
                main.m_replayTrack.Value = System.Math.Min(m_replayStep, main.m_replayTrack.Maximum)
            End If

            main.m_replayPlay.Text = If(m_replayPlaying, "暂停", "播放")
        End Sub

        Friend Sub onReplayPlay(sender As Object, e As EventArgs)
            If m_replay Is Nothing Then Return

            If m_replayPlaying Then
                m_replayPlaying = False
                m_replayTimer.Stop()
            Else
                ' 已经停在最后一帧时，从第一步重新播
                If m_replayStep >= m_replay.Steps - 1 Then
                    m_replayStep = 0
                    Call updateReplayFrame()
                End If

                m_replayPlaying = True
                m_replayTimer.Interval = CInt(System.Math.Max(20, main.m_replaySpeed.Value))
                m_replayTimer.Start()
            End If

            Call updateReplayPanel()
        End Sub

        Friend Sub onReplaySpeedChanged(sender As Object, e As EventArgs)
            m_replayTimer.Interval = CInt(System.Math.Max(20, main.m_replaySpeed.Value))
        End Sub

        Friend Sub onReplayTrackScroll(sender As Object, e As EventArgs)
            If m_replay Is Nothing Then Return

            m_replayStep = main.m_replayTrack.Value
            Call updateReplayFrame()
        End Sub

        Friend Sub onReplayFirst(sender As Object, e As EventArgs)
            Call seekReplay(0)
        End Sub

        Friend Sub onReplayPrev(sender As Object, e As EventArgs)
            Call seekReplay(m_replayStep - 1)
        End Sub

        Friend Sub onReplayNext(sender As Object, e As EventArgs)
            Call seekReplay(m_replayStep + 1)
        End Sub

        Friend Sub onReplayLast(sender As Object, e As EventArgs)
            If m_replay Is Nothing Then Return

            Call seekReplay(m_replay.Steps - 1)
        End Sub

        Friend Sub seekReplay(stepIndex As Integer)
            If m_replay Is Nothing Then Return

            m_replayPlaying = False
            m_replayTimer.Stop()
            m_replayStep = System.Math.Max(0, System.Math.Min(stepIndex, m_replay.Steps - 1))

            Call updateReplayFrame()
        End Sub

        Friend Sub onReplayClear(sender As Object, e As EventArgs)
            Call stopReplay(clearHighlight:=True)

            main.m_replayText.Text = "已清除回放高亮"
        End Sub

        ''' <summary>停止回放（可选把点云恢复成未高亮状态）。</summary>
        Private Sub stopReplay(clearHighlight As Boolean)
            m_replayTimer.Stop()
            m_replayPlaying = False

            If clearHighlight AndAlso m_highlighter IsNot Nothing Then
                Call m_highlighter.Reset()
                Call main.m_canvas.UpdatePointCloud(m_highlighter.Points)
            End If

            m_replay = Nothing
            m_highlighter = Nothing
            m_replayStep = -1

            Call clearStimulusMarker()
            Call updateReplayPanel()
        End Sub

        ''' <summary>把"电极位置"用一个大号十字标出来。</summary>
        Private Sub setStimulusMarker(neuron As Integer)
            If m_dataset Is Nothing OrElse neuron < 0 OrElse neuron >= m_dataset.Units Then Return
            If Not m_dataset.HasPosition(neuron) Then Return

            Dim lines As New List(Of LineSegment)(3)
            Dim position As FlywireAI.FAFBv783.NeuronPosition = m_dataset.GetPosition(neuron)
            Dim size As Double = 9000.0
            Dim color As Color = Color.FromArgb(34, 211, 238)

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

                Call lines.Add(New LineSegment(a, b, color))
            Next

            Call main.m_canvas.UpdateConnections(lines)

            main.m_canvas.ShowConnections = True
        End Sub

        ''' <summary>撤掉电极标记（回放高亮不清除，那是点云的颜色）。</summary>
        Private Sub clearStimulusMarker()
            If main.m_scene Is Nothing Then Return

            If RibbonMenu.ToggleConnection Then
                Call main.m_canvas.UpdateConnections(main.m_scene.Lines)
            Else
                Call main.m_canvas.UpdateConnections(New LineSegment() {})
            End If
        End Sub

        ''' <summary>仿真步数改变（下一次刺激生效）。</summary>
        Friend Sub onStimulusStepsChanged(sender As Object, e As EventArgs)
            m_config.TimeSteps = RibbonMenu.SimulationSteps
        End Sub

        ''' <summary>
        ''' 场景被重建之后重新绑定高亮器。
        ''' </summary>
        ''' <remarks>
        ''' 切换着色维度、改筛选条件都会重建点云，此时基准颜色已经变了 ——
        ''' 继续用旧的高亮器会把"上一套配色下的颜色"写进新点云。
        ''' </remarks>
        Friend Sub rebindHighlighter()
            If main.m_scene Is Nothing OrElse m_dataset Is Nothing OrElse m_replay Is Nothing Then Return

            m_highlighter = New ReplayHighlighter(main.m_scene, m_dataset.Units, main.isHeatMap())

            If m_replayStep < 0 Then m_replayStep = 0

            Call updateReplayFrame()
        End Sub

#End Region

#Region "offline snapshot"

        ''' <summary>出图模式下的刺激规格（``Nothing`` 表示这次出图不做刺激）。</summary>
        Friend m_snapshotStimulus As SnapshotStimulus?

        ''' <summary>
        ''' 解析 ``stim=&lt;neuron>:&lt;radiusUm>:&lt;current>:&lt;step>`` 形式的出图参数。
        ''' </summary>
        ''' <remarks>
        ''' ``neuron`` 为 -1 时取"出度最大的神经元"，``step`` 为 -1 时取"最活跃的那一步"。
        ''' </remarks>
        Friend Sub parseSnapshotStimulus(spec As String)
            Dim parts As String() = spec.Split(":"c)

            If parts.Length < 3 Then
                Throw New ArgumentException($"无法解析刺激规格 '{spec}'（应形如 67232:100:1.5:0）")
            End If

            Dim radiusUm As Double = 100
            Dim currentStim As Double = CurrentFloor
            Dim stepIndex As Integer = -1

            Double.TryParse(parts(1), radiusUm)
            Double.TryParse(parts(2), currentStim)
            Integer.TryParse(If(parts.Length > 3, parts(3), "-1"), stepIndex)

            m_snapshotStimulus = New SnapshotStimulus With {
            .Neuron = CInt(Val(parts(0))),
            .RadiusNm = radiusUm * 1000.0,
            .Current = If(currentStim > 0, currentStim, CurrentFloor),
            .StepIndex = stepIndex
        }
        End Sub

        ''' <summary>
        ''' 出图模式：在后台跑一次电刺激，随后在 UI 线程把指定步的高亮画出来并抓帧。
        ''' </summary>
        ''' <remarks>
        ''' 这是唯一能<b>离线验证回放渲染</b>的入口：自检模式（--stimulate）只覆盖数据链路，
        ''' 不碰 GPU；而回放的"点亮 + 放大"最终要落到顶点着色器上，只能靠出图来确认。
        ''' 
        ''' 首次运行需要装配连接组（读 261 MB 连接表，约 30 s），因此它是离线诊断模式，
        ''' 不追求交互速度 —— 但仿真本身与交互模式走的是<b>同一段代码</b>
        ''' （<see cref="BrainStimulator"/>），所以出图结果能代表交互观感。
        ''' </remarks>
        Friend Sub applySnapshotStimulus()
            If Not m_snapshotStimulus.HasValue Then Return

            Dim spec As SnapshotStimulus = m_snapshotStimulus.Value

            m_snapshotStimulus = Nothing

            Dim output As String = main.m_snapshotPath

            ' 出图是离线诊断模式：把进度与错误都写到标准输出，便于无人值守地看结果
            Call Console.Out.WriteLine($"snapshot stimulus: neuron={spec.Neuron}, radius={spec.RadiusNm / 1000.0:F0} um, " &
                                   $"current={spec.Current:F2}, step={spec.StepIndex}")
            Call Console.Out.Flush()

            Task.Run(Function() As StimulationReplay
                         Dim neuron As Integer = spec.Neuron

                         If neuron < 0 Then
                             neuron = BrainSceneBuilder.FindBusiestNeuron(m_dataset)
                         End If

                         Dim stimulator As New BrainStimulator(m_config, m_dataset)

                         If Not stimulator.Prepare(Sub(message) Trace.WriteLine(message), CancellationToken.None) Then
                             Throw New InvalidOperationException($"刺激引擎装配失败: {stimulator.LastError}")
                         End If

                         Dim replay As StimulationReplay = stimulator.Stimulate(neuron, spec.RadiusNm, spec.Current, 0,
                                                                            Sub(message) Trace.WriteLine(message))

                         Call StimulationReport.Write(replay, m_config.ResolveActivityDir(), m_dataset.Index)

                         Return replay
                     End Function) _
            .ContinueWith(
                Sub(task As Task(Of StimulationReplay))
                    If task.IsFaulted Then
                        Call Console.Out.WriteLine($"snapshot stimulation FAILED: " &
                                                   $"{task.Exception?.GetBaseException()?.GetType().Name}: " &
                                                   $"{task.Exception?.GetBaseException()?.Message}")
                        Call Console.Out.WriteLine(task.Exception?.GetBaseException()?.StackTrace)
                        Call Console.Out.Flush()
                    Else
                        Dim replay As StimulationReplay = task.Result

                        m_replay = replay
                        m_replayStep = If(spec.StepIndex >= 0,
                                          System.Math.Min(spec.StepIndex, replay.Steps - 1),
                                          replay.PeakStep)
                        m_highlighter = New ReplayHighlighter(main.m_scene, m_dataset.Units, main.isHeatMap())

                        Call updateReplayFrame()
                        Call updateReplayPanel()

                        Call Console.Out.WriteLine($"stimulation: {replay.Describe()}")
                        Call Console.Out.WriteLine($"replay highlight mode: heatMap={ main.isHeatMap()}, " &
                                                   $"dimension={If(main.m_colorizer Is Nothing, "none", main.m_colorizer.Dimension.ToString)}, " &
                                                   $"embeddedColor={ main.m_canvas.UseEmbeddedColor}")
                        Call Console.Out.WriteLine($"rendering replay step {m_replayStep + 1}/{replay.Steps}, " &
                                                   $"active {replay.ActivePerStep(m_replayStep):N0}, " &
                                                   $"highlighted {replay.StepActive(m_replayStep).Length:N0} neurons")
                        Call Console.Out.Flush()
                    End If

                    If output IsNot Nothing Then
                        main.m_snapshotPath = output
                        Call main.captureAndExit()
                    End If
                End Sub,
                TaskScheduler.FromCurrentSynchronizationContext())
        End Sub

#End Region

#Region "command line probe"

        ''' <summary>
        ''' 电刺激自检（``--stimulate &lt;报告.txt&gt; [数据目录] [神经元] [强度列表]``）。
        ''' </summary>
        ''' <remarks>
        ''' 目的有两个：
        ''' 
        ''' 1. <b>标定强度映射</b>：全脑的权重经过结构归一化，单个神经元的持续放电能扩散多远
        '''    只能实测 —— 这里对同一神经元扫一串强度，把"注入强度 → 逐步激活规模"打出来，
        '''    界面上的按住时长映射区间（"StrengthFloor" ~ "StrengthCeiling"）
        '''    就是据此定的；
        ''' 2. <b>验证回放数据链路</b>：运行、取逐步激活、落盘、并断言逐步清单与统计一致。
        ''' </remarks>
        Friend Sub runStimulationProbe(args As String())
            Dim report As New StringBuilder()
            Dim failures As Integer = 0
            Dim reportFile As String = If(args.Length > 2, args(2), Path.Combine(AppContext.BaseDirectory, "stimulation-report.txt"))

            m_config.DataDir = If(args.Length > 3, args(3), m_config.DataDir)

            m_config.TimeSteps = 30
            m_config.KeepHistory = True
            m_config.UseGpu = True
            m_config.UseFusedStep = True
            m_config.ResidentPrecision = LifResidentPrecision.Double64
            m_config.StimulationNeurons = 5000

            ' 扫描的是"募集半径"而不是电流：单点刺激在归一化权重下无法传播，
            ' 真正决定激活规模的是被电极募集到的那一群神经元有多大
            Dim radii As New List(Of Double)()

            If args.Length > 5 Then
                For Each part As String In args(5).Split(","c)
                    Dim value As Double

                    If Double.TryParse(part, value) AndAlso value > 0 Then
                        Call radii.Add(value)
                    End If
                Next
            End If

            If radii.Count = 0 Then
                radii.AddRange(New Double() {25000, 50000, 100000, 200000})
            End If

            Dim current As Double = CurrentFloor

            If args.Length > 6 Then
                Dim parsed As Double

                If Double.TryParse(args(6), parsed) AndAlso parsed > 0 Then
                    current = parsed
                End If
            End If

            Try
                Call report.AppendLine("Neuropils electrical stimulation probe")
                Call report.AppendLine($"data dir : {m_config.DataDir}")
                Call report.AppendLine($"time     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                Call report.AppendLine($"steps    : {m_config.TimeSteps}")
                Call report.AppendLine()

                ' 直接走数据层：不建窗体、不装配可视化场景，把注意力集中在仿真本身
                Dim loader As New BrainDatasetLoader(m_config)
                Dim dataset As BrainDataset = loader.Load(Sub(message) Call report.AppendLine($"      {message}"))

                Dim probeNeuron As Integer

                If args.Length > 4 AndAlso Integer.TryParse(args(4), probeNeuron) Then
                    ' 用命令行指定的神经元
                Else
                    probeNeuron = BrainSceneBuilder.FindBusiestNeuron(dataset)
                End If

                Call report.AppendLine($"      probe neuron: #{probeNeuron} (root_id {dataset.Index.GetRootId(probeNeuron)}, " &
                                   $"neuropil {dataset.GetNeuropilName(probeNeuron)}, " &
                                   $"nt {dataset.Neurotransmitters(probeNeuron)})")
                Call report.AppendLine()

                Dim stimulator As New BrainStimulator(m_config, dataset)
                Dim prepared As Boolean = stimulator.Prepare(Sub(message) Call report.AppendLine($"      {message}"),
                                                         CancellationToken.None)

                Call check(report, failures, "stimulator is prepared", prepared, True)
                Call check(report, failures, "gain is positive", stimulator.Gain > 0, True)

                Call report.AppendLine()
                Call report.AppendLine($"---- recruitment radius sweep (current = {current:F2}) ----")
                Call report.AppendLine("      radius_um   recruited   total    active   peak   fallback   wall_ms")
                Call report.AppendLine()

                Dim best As StimulationReplay = Nothing
                Dim first As StimulationReplay = Nothing

                For Each radius As Double In radii
                    Dim replay As StimulationReplay = stimulator.Stimulate(probeNeuron, radius, current, 0, Nothing)

                    Call report.AppendLine($"      {radius / 1000.0,9:F0} {replay.RecruitedNeurons,11:N0} {replay.TotalSpikes,8:N0} " &
                                       $"{replay.ActiveUnionCount,8:N0} {replay.PeakActive,6:N0} " &
                                       $"{replay.FallbackSteps,10} {replay.WallMs,9:N0}")

                    If first Is Nothing Then first = replay
                    If best Is Nothing OrElse replay.ActiveUnionCount > best.ActiveUnionCount Then
                        best = replay
                    End If
                Next

                Call report.AppendLine()
                Call check(report, failures, "at least one strength activates neurons",
                       best IsNot Nothing AndAlso best.TotalSpikes > 0, True)

                ' ---- 响应曲线数据（膜电位分析回放）----
                If first IsNot Nothing Then
                    Call report.AppendLine()
                    Call report.AppendLine("---- response potential (analysis replay) ----")
                    Call report.AppendLine($"      signal: {first.ResponseSignal}")

                    Call check(report, failures, "membrane potential covers every responding neuron",
                           first.ResponseNeurons.Length, first.ActiveUnionCount)
                    Call check(report, failures, "every potential row has one value per time step",
                           If(first.ResponsePotential.Length = first.ResponseNeurons.Length AndAlso
                              first.ResponsePotential.All(Function(row) row IsNot Nothing AndAlso row.Length = first.Steps),
                              "ok", "bad"), "ok")
                    Call check(report, failures, "analysis replay reproduces the spike train",
                           If(first.AnalysisMatches.HasValue, If(first.AnalysisMismatches = 0, "identical", $"mismatch={first.AnalysisMismatches}"), "not run"),
                           "identical")

                    If first.ResponseNeurons.Length > 0 Then
                        Dim sample As Integer = first.ResponseNeurons(0)
                        Dim curve As Double() = first.ResponsePotential(0)

                        Call report.AppendLine($"      sample neuron #{sample}: " &
                                           String.Join(", ", curve.Select(Function(v) v.ToString("F3"))))
                    End If
                End If

                ' ---- 换个线程再刺激一次（这是"第一次正常、第二次报 CUDA_ERROR_INVALID_CONTEXT"的复现场景）----
                ' CUDA 的当前上下文是线程局部状态：线程池把第二次任务调度到另一条线程时，
                ' 那条线程没有绑定上下文，分配显存就会失败。这里刻意用另一条线程重跑同样的刺激，
                ' 并要求结果与第一次逐位一致。
                Call report.AppendLine()
                Call report.AppendLine("---- cross thread re-run (regression: CUDA_ERROR_INVALID_CONTEXT) ----")

                Dim second As StimulationReplay = Nothing
                Dim failure As String = Nothing
                Dim radius0 As Double = radii(0)

                Call Task.Run(
                Sub()
                    Try
                        second = stimulator.Stimulate(probeNeuron, radius0, current, 0, Nothing)
                    Catch ex As Exception
                        failure = $"{ex.GetType().Name}: {ex.Message}"
                    End Try
                End Sub).Wait()

                Call check(report, failures, "a second stimulation on another thread succeeds",
                       If(failure, "ok"), "ok")

                If failure IsNot Nothing OrElse second Is Nothing Then
                    Call report.AppendLine($"      [FAIL] {failure}")
                Else
                    Call report.AppendLine($"      {radius0 / 1000.0,9:F0} {second.RecruitedNeurons,11:N0} {second.TotalSpikes,8:N0} " &
                                       $"{second.ActiveUnionCount,8:N0} {second.PeakActive,6:N0} " &
                                       $"{second.FallbackSteps,10} {second.WallMs,9:N0}")

                    Call check(report, failures, "cross thread re-run is bit identical to the first run",
                           If(first Is Nothing, -1.0, first.TotalSpikes), second.TotalSpikes)
                End If

                If best IsNot Nothing Then
                    Call report.AppendLine()
                    Call report.AppendLine($"---- per step detail (best = strength {best.Strength:F2}) ----")
                    Call report.AppendLine("      step   active    spikes   cumulative")

                    Dim cumulative As Double = 0

                    For t As Integer = 0 To best.Steps - 1
                        cumulative += best.SpikesPerStep(t)

                        Call report.AppendLine($"      {t + 1,4} {best.ActivePerStep(t),8:N0} {best.SpikesPerStep(t),9:N0} {cumulative,12:N0}")
                    Next

                    Call report.AppendLine()
                    checkStepConsistency(report, failures, best)

                    Dim dir As String = StimulationReport.Write(best, m_config.ResolveActivityDir(), dataset.Index)

                    Call report.AppendLine($"      report dir: {dir}")

                    For Each filePath As String In best.ReportFiles
                        Call report.AppendLine($"        {Path.GetFileName(filePath)} ({fileLineCount(filePath)} lines)")
                    Next

                    Call check(report, failures, "replay csv written", Directory.Exists(dir), True)
                    Call check(report, failures, "active neuron rows == sum of per step active",
                           activeNeuronRows(best), best.ActivePerStep.Sum())
                End If
            Catch ex As Exception
                Call report.AppendLine()
                Call report.AppendLine($"[FATAL] {ex.GetType().Name}: {ex.Message}")
                Call report.AppendLine(ex.StackTrace)

                failures += 1
            End Try

            Call report.AppendLine()
            Call report.AppendLine($"result: {(If(failures = 0, "PASS", $"FAIL ({failures})"))}")

            Dim text As String = report.ToString()

            Try
                Call File.WriteAllText(reportFile, text, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
            Catch ex As Exception
                Trace.WriteLine($"unable to write the stimulation report: {ex.Message}")
            End Try

            Call Console.Out.Write(text)
            Call Console.Out.Flush()

            Call Environment.Exit(If(failures = 0, 0, 1))
        End Sub

        ''' <summary>逐步激活清单必须与实际脉冲数一致（回放数据的正确性底线）。</summary>
        Private Shared Sub checkStepConsistency(report As StringBuilder, ByRef failures As Integer, replay As StimulationReplay)
            Dim mismatch As Integer = 0

            For t As Integer = 0 To replay.Steps - 1
                Dim active As Integer() = replay.StepActive(t)

                ' 每个发放的神经元至少贡献 1 个脉冲，因此激活数不应超过脉冲数
                If active.Length > replay.SpikesPerStep(t) + 0.5 Then mismatch += 1
                If active.Length <> replay.ActivePerStep(t) Then mismatch += 1
            Next

            Call check(report, failures, "per step active lists agree with the counters", mismatch, 0)
        End Sub

        Private Shared Function activeNeuronRows(replay As StimulationReplay) As Integer
            Dim rows As Integer = 0

            For t As Integer = 0 To replay.Steps - 1
                rows += replay.StepActive(t).Length
            Next

            Return rows
        End Function

        ''' <remarks>
        ''' 形参不能叫 ``file``：会遮蔽 <see cref="System.IO.File"/>（VB 不区分大小写）。
        ''' </remarks>
        Private Shared Function fileLineCount(path As String) As Integer
            If Not IO.File.Exists(path) Then Return 0

            Dim lines As Integer = 0

            For Each line As String In IO.File.ReadLines(path)
                lines += 1
            Next

            Return lines
        End Function

#End Region

    End Class
End Namespace