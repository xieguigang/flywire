Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Text
Imports FlywireSnake
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

    ''' <summary>
    ''' ``--snake &lt;报告.txt&gt; [数据目录] [训练局数] [每局 tick 数] [评估局数]``
    ''' </summary>
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
