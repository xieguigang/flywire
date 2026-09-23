Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports FlywireAI.Connectome
Imports FlywireSnake
Imports Neuropils.Rendering
Imports Snake2

''' <summary>
''' "果蝇大脑玩贪吃蛇"的命令行自检：跑训练与评估，把三组对照的得分打出来。
''' </summary>
''' <remarks>
''' 与观战窗口共用同一套装配 / 会话 / 解码器代码，因此这里量出来的分数
''' 就是窗口里那条蛇的真实水平。
''' 
''' 三组对照：
''' <list type="bullet">
'''   <item><b>未训练</b>：随机读出 —— 下界；</item>
'''   <item><b>教师</b>：贪心追食物的示范策略 —— 操作逻辑的上限参考；</item>
'''   <item><b>训练后</b>：由果蝇大脑（afferent 注入 → efferent 读出）驱动的蛇。</item>
''' </list>
''' </remarks>
Partial Public Class FormMain

#Region "观战窗口"

    ''' <summary>果蝇大脑玩贪吃蛇的观战窗口（单实例复用）。</summary>
    Private m_snakeForm As SnakeBrainForm

    ''' <summary>装配好的游戏用大脑环境（连接组 + CSR + 标定，约 30 秒，只做一次）。</summary>
    Private m_snakePlayground As SnakePlayground

    ''' <summary>把三维点云切换成"喂给蛇的大脑活动"的高亮器。</summary>
    Private m_snakeHighlighter As ReplayHighlighter

    ''' <summary>逐神经元权重缓冲（跨 tick 复用，避免每帧分配 1.1 MB）。</summary>
    Private m_snakeMask As Double()

    ''' <summary>
    ''' 打开观战窗口：首次需要装配连接组（约 30 秒），在后台完成并显示进度。
    ''' </summary>
    Private Sub onOpenSnakeWindow(sender As Object, e As LinkLabelLinkClickedEventArgs)
        Call openSnakeWindow()
    End Sub

    ''' <summary>打开观战窗口（链接与命令行自检共用）。</summary>
    Private Sub openSnakeWindow()
        If m_dataset Is Nothing Then Return

        If m_snakeForm IsNot Nothing AndAlso Not m_snakeForm.IsDisposed Then
            Call m_snakeForm.BringToFront()

            Return
        End If

        If m_snakePlayground IsNot Nothing Then
            Call showSnakeWindow(m_snakePlayground)

            Return
        End If

        m_statusText.Text = "正在装配游戏用的果蝇大脑（连接组 → CSR，约 30 秒）..."
        m_progress.Visible = True
        m_progress.Style = ProgressBarStyle.Marquee

        Task.Run(
            Function() As SnakePlayground
                ' 顺带把 GPU 后端注册上：逐 tick 推理用融合路径最快
                Call GpuRuntime.TryRegister(m_config, AddressOf onLoadProgress)

                Return SnakePlayground.Create(m_config, AddressOf onLoadProgress)
            End Function) _
            .ContinueWith(
                Sub(task As Task(Of SnakePlayground))
                    m_progress.Visible = False
                    m_progress.Style = ProgressBarStyle.Continuous

                    If task.IsFaulted Then
                        m_statusText.Text = "装配果蝇大脑失败"
                        Call MessageBox.Show(Me, $"{task.Exception?.GetBaseException()?.Message}",
                                             "无法启动观战窗口", MessageBoxButtons.OK, MessageBoxIcon.Error)

                        Return
                    End If

                    m_snakePlayground = task.Result

                    Call showSnakeWindow(m_snakePlayground)
                End Sub,
                TaskScheduler.FromCurrentSynchronizationContext())
    End Sub

    ''' <summary>创建观战窗口并接上"三维活动可视化"。</summary>
    Private Sub showSnakeWindow(playground As SnakePlayground)
        Dim brain As SnakeBrain = playground.CreateBrain()
        Dim decoder As SnakeDecoder = loadTrainedDecoder(brain)

        If m_snakeHighlighter Is Nothing AndAlso m_scene IsNot Nothing Then
            m_snakeHighlighter = New ReplayHighlighter(m_scene, m_dataset.Units, isHeatMap())
        End If

        If m_snakeMask Is Nothing Then
            m_snakeMask = New Double(m_dataset.Units - 1) {}
        End If

        ' 用局部变量持有窗体：FormClosed 里 m_snakeForm 会被清空，只有它能用来解绑事件
        Dim brainForm As New SnakeBrainForm(playground, brain, decoder)

        AddHandler brainForm.BrainActivityChanged, AddressOf onSnakeBrainActivity
        AddHandler brainForm.FormClosed,
            Sub()
                ' 窗体自己已经停机（停定时器 + 释放显存），这里把主窗口这一侧也收干净
                RemoveHandler brainForm.BrainActivityChanged, AddressOf onSnakeBrainActivity

                If m_snakeForm Is brainForm Then m_snakeForm = Nothing

                ' 关窗后把点云恢复成原来的着色：否则最后点亮的那批神经元会一直亮着
                If m_snakeHighlighter IsNot Nothing Then
                    Call m_snakeHighlighter.Reset()
                    Call m_canvas.UpdatePointCloud(m_snakeHighlighter.Points)

                    m_snakeHighlighter = Nothing
                End If
            End Sub

        m_snakeForm = brainForm

        Call brainForm.Show(Me)

        m_statusText.Text = $"观战窗口已打开：{brain.FlowSummary}"

        ' 命令行自检：跑够指定帧数后抓屏退出
        If m_snakeProbeTicks > 0 Then
            Call startSnakeProbeCapture(brainForm)
        End If
    End Sub

#End Region

#Region "观战窗口的自动化验证"

    ''' <summary>``--snake-window &lt;png&gt; [数据目录] [tick 数]`` 的抓屏状态。</summary>
    Private m_snakeProbePng As String = Nothing
    Private m_snakeProbeTicks As Integer
    Private m_snakeProbeSeen As Integer
    Private m_snakeProbeCaptured As Boolean

    ''' <summary>等待观战窗口跑够帧数，然后把两个窗口一起抓屏写盘再退出。</summary>
    ''' <remarks>
    ''' 抓的是<b>屏幕上真实的两个窗口</b>（主窗口的三维点云 + 观战窗口），
    ''' 因此它同时验证了"游戏实时画面"与"三维神经元活动点亮"这两条链路。
    ''' </remarks>
    Private Sub startSnakeProbeCapture(snakeForm As SnakeBrainForm)
        AddHandler snakeForm.BrainActivityChanged,
            Sub(activeNeurons As Integer(), frame As SnakeStep)
                ' 抓屏后的重入事件直接丢掉，帧数才不会被 DoEvents 抽出来的定时器消息灌大
                If m_snakeProbeCaptured Then Return

                m_snakeProbeSeen += 1

                If m_snakeProbeSeen < m_snakeProbeTicks Then Return

                m_snakeProbeCaptured = True

                ' 先停表：否则 DoEvents 会重入定时器，抓到的帧数与请求的不一致
                Call snakeForm.PauseForProbe()

                ' 让最后一帧先画出来，再抓屏
                Call Application.DoEvents()
                Call Threading.Thread.Sleep(400)
                Call Application.DoEvents()

                Call captureScreen(m_snakeProbePng)
                Call captureWindow(snakeForm, IO.Path.ChangeExtension(m_snakeProbePng, ".window.png"))
                Call Console.Out.WriteLine($"snake window probe: {m_snakeProbeSeen} ticks rendered, " &
                                           $"active neurons={activeNeurons.Length}, score={frame.Score}, " &
                                           $"saved={m_snakeProbePng}")
                Call Console.Out.Flush()
                Call Environment.Exit(0)
            End Sub
    End Sub

    ''' <summary>
    ''' 把观战窗口<b>自身</b>画成一张图。
    ''' </summary>
    ''' <remarks>
    ''' 与 <see cref="captureScreen"/> 的区别是它与屏幕上的 z 序无关：
    ''' 窗口被别人挡着也照样能拿到内容，因此适合在自动化里核对
    ''' "右侧的感觉通道 / 运动读出到底有没有画出数据"。
    ''' </remarks>
    Private Shared Sub captureWindow(window As Form, outputFile As String)
        Using bmp As New Bitmap(window.Width, window.Height)
            Call window.DrawToBitmap(bmp, New Rectangle(0, 0, bmp.Width, bmp.Height))
            Call bmp.Save(outputFile, System.Drawing.Imaging.ImageFormat.Png)
        End Using
    End Sub

    ''' <summary>把整个虚拟屏幕抓成一张图（两个窗口都在上面）。</summary>
    Private Shared Sub captureScreen(file As String)
        Dim bounds As Rectangle = Screen.PrimaryScreen.Bounds

        Using bmp As New Bitmap(bounds.Width, bounds.Height)
            Using g As Graphics = Graphics.FromImage(bmp)
                Call g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size)
            End Using

            Call bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png)
        End Using
    End Sub

    ''' <summary>尽量用已训练好的解码器开局（没有就用随机读出）。</summary>
    Private Function loadTrainedDecoder(brain As SnakeBrain) As SnakeDecoder
        Try
            ' 局部变量不能叫 file：会遮蔽 System.IO.File（VB 不区分大小写）
            Dim decoderFile As String = IO.Path.Combine(m_config.ResolveActivityDir(), "snake", "snake_decoder.csv")

            If IO.File.Exists(decoderFile) Then
                Return SnakeDecoder.Load(decoderFile, brain.MotorFeatures.Length)
            End If
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"unable to load the trained decoder: {ex.Message}")
        End Try

        Return New SnakeDecoder(brain.MotorFeatures.Length)
    End Function

    ''' <summary>
    ''' 把"果蝇大脑此刻发放的神经元"实时点亮到三维点云上。
    ''' </summary>
    ''' <remarks>
    ''' 与本窗口的响应对齐：会话在 UI 线程上推进，因此这个回调也在 UI 线程，
    ''' 可以直接更新画布而不用跨线程封送。
    ''' </remarks>
    Private Sub onSnakeBrainActivity(activeNeurons As Integer(), frame As SnakeStep)
        If m_scene Is Nothing OrElse m_dataset Is Nothing Then Return

        ' 关窗之后不该再点亮任何东西：高亮器是在这里按需重建的，
        ' 少了这道闸，残留事件会让刚被 Reset 的点云又亮起来（看起来"关掉了还在闪"）
        If m_snakeForm Is Nothing Then Return

        If m_snakeHighlighter Is Nothing Then
            m_snakeHighlighter = New ReplayHighlighter(m_scene, m_dataset.Units, isHeatMap())
        End If

        If m_snakeMask Is Nothing OrElse m_snakeMask.Length <> m_dataset.Units Then
            m_snakeMask = New Double(m_dataset.Units - 1) {}
        Else
            ' 只清上一帧动过的格子：整块清零在 13 万长度上每 tick 也要 1 MB 的写带宽
            Array.Clear(m_snakeMask, 0, m_snakeMask.Length)
        End If

        If activeNeurons IsNot Nothing Then
            For i As Integer = 0 To activeNeurons.Length - 1
                Dim neuron As Integer = activeNeurons(i)

                If neuron >= 0 AndAlso neuron < m_snakeMask.Length Then
                    m_snakeMask(neuron) = 1.0
                End If
            Next
        End If

        Call m_snakeHighlighter.ApplyMask(m_snakeMask, activeNeurons)
        Call m_canvas.UpdatePointCloud(m_snakeHighlighter.Points)
    End Sub

#End Region

    ''' <summary>
    ''' ``--snake &lt;报告.txt&gt; [数据目录] [训练局数] [每局 tick 数] [评估局数]
    ''' [每通道感觉神经元数] [感觉注入电流] [读出窗宽]``
    ''' </summary>
    ''' <remarks>
    ''' 后三个参数是"感觉 / 读出标定"的档位：调它们就是为了让大脑真的能感觉到游戏状态
    ''' （见 <see cref="SnakePlayground.SensorsPerChannel"/> 的说明），留成命令行参数是为了能一档一档地实测。
    ''' </remarks>
    Private Sub runSnakeProbe(args As String())
        Dim report As New StringBuilder()
        Dim failures As Integer = 0
        Dim reportFile As String = args(2)
        Dim episodes As Integer = 8
        Dim ticks As Integer = 300
        Dim evaluateRounds As Integer = 8

        m_config.DataDir = If(args.Length > 3 AndAlso args(3).Length > 0, args(3), DefaultDataDir)

        If args.Length > 4 Then Integer.TryParse(args(4), episodes)
        If args.Length > 5 Then Integer.TryParse(args(5), ticks)
        If args.Length > 6 Then Integer.TryParse(args(6), evaluateRounds)
        If episodes <= 0 Then episodes = 8
        If ticks <= 0 Then ticks = 300
        If evaluateRounds <= 0 Then evaluateRounds = 8

        Dim sensorsPerChannel As Integer = 0
        Dim sensorCurrent As Double = 0
        Dim featureWindow As Integer = 0

        If args.Length > 7 Then Integer.TryParse(args(7), sensorsPerChannel)
        If args.Length > 8 Then Double.TryParse(args(8), sensorCurrent)
        If args.Length > 9 Then Integer.TryParse(args(9), featureWindow)

        ' 游戏要跑很多 tick，逐 tick 回读对 CPU 后端来说太贵，因此默认尝试 GPU
        m_config.UseGpu = True
        m_config.KeepHistory = True
        m_config.UseFusedStep = True
        m_config.ResidentPrecision = Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork.LifResidentPrecision.Double64
        m_config.StimulationNeurons = 5000

        Dim timer As Stopwatch = Stopwatch.StartNew()

        Try
            Call report.AppendLine("FlywireSnake: 果蝇大脑驾驶贪吃蛇")
            Call report.AppendLine($"data dir : {m_config.DataDir}")
            Call report.AppendLine($"episodes : {episodes} x {ticks} ticks (评估 {evaluateRounds} 局)")
            Call report.AppendLine()

            Using host As New Form() With {.ClientSize = New Size(1, 1)}
                Dim playground As SnakePlayground = SnakePlayground.Create(
                    m_config,
                    Sub(message) Call report.AppendLine($"      [{timer.ElapsedMilliseconds,7:N0} ms] {message}"))

                ' 标定档位（没给就沿用 SnakePlayground 的默认值）
                If sensorsPerChannel > 0 Then playground.SensorsPerChannel = sensorsPerChannel
                If sensorCurrent > 0 Then playground.SensorCurrent = sensorCurrent
                If featureWindow > 0 Then playground.FeatureWindow = featureWindow

                Call report.AppendLine()
                Call report.AppendLine($"tuning    : 每通道感觉神经元 {playground.SensorsPerChannel} 个、" &
                                       $"注入电流 {playground.SensorCurrent}、读出窗宽 {playground.FeatureWindow} tick")
                Call report.AppendLine()

                ' 感觉 / 运动神经元的选取：先跑一局看通路是否活着
                Dim brain As SnakeBrain = playground.CreateBrain()
                Dim decoder As New SnakeDecoder(brain.MotorFeatures.Length)

                Call report.AppendLine($"      {brain.FlowSummary}")
                Call report.AppendLine($"      感觉通道神经元：{SnakeSensors.ChannelCount} 通道 x {brain.SensorNeurons(0).Length} 个")
                Call report.AppendLine($"      运动读出神经元：{brain.MotorFeatures.Length} 个（{brain.MotorGroups.Length} 组）")
                Call report.AppendLine()

                Dim session As SnakeSession = playground.CreateSession(brain, decoder, host)

                ' ---- 单步健全性检查：感觉电流进得去、运动神经元出得来 ----
                Call session.NewEpisode()

                Dim alive As Integer = 0
                Dim activeTotal As Double = 0
                Dim sensorChannels As Integer = 0
                Dim motorSpikes As Double = 0
                Dim motorActiveTotal As Double = 0

                For i As Integer = 1 To 40
                    Dim frame As SnakeStep = session.Tick()

                    activeTotal += frame.ActiveNeurons.Length
                    motorSpikes += frame.MotorSpikes.Sum()
                    motorActiveTotal += brain.MotorActiveCount

                    For c As Integer = 0 To frame.Sensors.Length - 1
                        If frame.Sensors(c) > 0 Then sensorChannels += 1
                    Next

                    If Not frame.Died Then alive += 1
                    If frame.Died Then Call session.NewEpisode()
                Next

                Call check(report, failures, "大脑每个 tick 都有神经元发放", activeTotal > 0, True)
                Call check(report, failures, "感觉通道确实被激活", sensorChannels > 0, True)
                Call check(report, failures, "运动读出通路是活的（运动神经元有放电）", motorSpikes > 0, True)
                Call report.AppendLine($"      40 tick 健全性检查：平均发放 {activeTotal / 40:N0} 个神经元 / tick，" &
                                       $"其中运动神经元 {motorActiveTotal / 40:F1} 个（{motorSpikes / 40:F1} 个脉冲），" &
                                       $"感觉通道累计激活 {sensorChannels} 次，存活 {alive} tick")
                Call report.AppendLine()

                ' ---- 训练 + 三组对照 ----
                Dim training As SnakeTrainingReport = playground.Train(
                    episodes, ticks,
                    Sub(message) Call report.AppendLine($"      [{timer.ElapsedMilliseconds,7:N0} ms] {message}"))

                Call report.AppendLine()
                Call report.AppendLine($"      {training.Describe()}")
                Call report.AppendLine()

                Call check(report, failures, "训练采集到样本", training.Samples > 0, True)
                Call check(report, failures, "解码器学到了示范动作", training.Accuracy > 0.45, True)
                Call check(report, failures, "教师策略能吃到食物", training.TeacherScore > 0, True)

                ' 训练后的解码器另跑一轮评估（避免与训练用的局数重合）
                Dim finalScore As Double = playground.Evaluate(decoder, brain, evaluateRounds, ticks)

                Call report.AppendLine($"      训练后再评估 {evaluateRounds} 局：均分 {finalScore:F2}")

                ' ---- 存档：把训练好的解码器与训练报告落盘，窗口可以直接用 ----
                Dim outputDir As String = Path.Combine(m_config.ResolveActivityDir(), "snake")
                Dim decoderFile As String = Path.Combine(outputDir, "snake_decoder.csv")
                Dim summaryFile As String = Path.Combine(outputDir, "snake_training.csv")

                Call Directory.CreateDirectory(outputDir)
                Call decoder.Save(decoderFile)

                Dim csv As New StringBuilder()

                Call csv.AppendLine("key,value")
                Call csv.AppendLine($"generated_at,{DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                Call csv.AppendLine($"samples,{training.Samples}")
                Call csv.AppendLine($"accuracy,{training.Accuracy}")
                Call csv.AppendLine($"untrained_score,{training.UntrainedScore}")
                Call csv.AppendLine($"teacher_score,{training.TeacherScore}")
                Call csv.AppendLine($"trained_score,{training.TrainedScore}")
                Call csv.AppendLine($"final_score,{finalScore}")
                Call csv.AppendLine($"episodes,{episodes}")
                Call csv.AppendLine($"ticks,{ticks}")
                Call csv.AppendLine($"motor_neurons,{brain.MotorFeatures.Length}")
                Call csv.AppendLine($"sensors_per_channel,{brain.SensorNeurons(0).Length}")
                Call csv.AppendLine($"elapsed_ms,{timer.ElapsedMilliseconds}")

                Call File.WriteAllText(summaryFile, csv.ToString, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))

                Call report.AppendLine($"      解码器权重: {decoderFile}")
                Call report.AppendLine($"      训练报告  : {summaryFile}")
            End Using
        Catch ex As Exception
            Call report.AppendLine()
            Call report.AppendLine($"[FATAL] {ex.GetType().Name}: {ex.Message}")
            Call report.AppendLine(ex.StackTrace)

            failures += 1
        End Try

        timer.Stop()

        Call report.AppendLine()
        Call report.AppendLine($"total elapsed: {timer.ElapsedMilliseconds:N0} ms")
        Call report.AppendLine($"result: {(If(failures = 0, "PASS", $"FAIL ({failures})"))}")

        Dim text As String = report.ToString()

        Try
            Call File.WriteAllText(reportFile, text, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
        Catch ex As Exception
            Trace.WriteLine($"unable to write the snake report: {ex.Message}")
        End Try

        Call Console.Out.Write(text)
        Call Console.Out.Flush()

        Call Environment.Exit(If(failures = 0, 0, 1))
    End Sub

End Class
