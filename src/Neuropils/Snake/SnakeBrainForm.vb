Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports FlywireSnake
Imports Snake2

''' <summary>
''' 观战窗口：看果蝇大脑玩贪吃蛇的实时画面。
''' </summary>
''' <remarks>
''' 左半屏是游戏本身的画面（直接调用 Snake2 的 <see cref="Render.Draw"/>，
''' 与玩家手动玩时看到的是同一套渲染），右半屏是"大脑此刻在做什么"：
''' 
''' <list type="bullet">
'''   <item><b>感觉通道</b>：16 个通道的强度 —— 大脑看到的食物方位 / 危险；</item>
'''   <item><b>运动读出</b>：4 组运动神经元的脉冲数与解码出的走向；</item>
'''   <item><b>脑活动</b>：本 tick 发放的神经元总数，并通过 <see cref="BrainActivityChanged"/>
'''         事件送到主窗口的三维点云上点亮。</item>
''' </list>
''' 
''' 会话在 UI 线程上推进（每 tick 只有两次内核启动，几毫秒量级），
''' 训练在后台线程上跑，完成后把新解码器换上。
''' </remarks>
Public Class SnakeBrainForm
    Inherits Form

    Private ReadOnly m_playground As SnakePlayground

    Private m_brain As SnakeBrain
    Private m_session As SnakeSession

    Private m_gamePanel As Panel
    Private m_sensors As ChannelBars
    Private m_motors As ChannelBars
    Private m_status As Label
    Private m_detail As Label
    Private m_training As Label
    Private ReadOnly m_timer As New Timer()
    Private m_speedBox As NumericUpDown
    Private m_tickBox As NumericUpDown
    Private m_playButton As Button

    ''' <summary>大脑活动事件：&lt;本 tick 发放的神经元, 本 tick 的完整读数&gt;。</summary>
    Public Event BrainActivityChanged(activeNeurons As Integer(), frame As SnakeStep)

    ''' <summary>本窗口所用的游戏会话（主窗口也用它拿活动读数）。</summary>
    Public ReadOnly Property Session As SnakeSession
        Get
            Return m_session
        End Get
    End Property

    Public Sub New(playground As SnakePlayground,
                   brain As SnakeBrain,
                   decoder As SnakeDecoder,
                   Optional autoStart As Boolean = True)

        If playground Is Nothing Then Throw New ArgumentNullException(NameOf(playground))

        m_playground = playground
        m_brain = brain

        Call initializeUi()
        Call resetSession(decoder, autoStart)
    End Sub

#Region "界面"

    Private Sub initializeUi()
        Me.Text = "果蝇大脑玩贪吃蛇 — 实时观战"
        Me.StartPosition = FormStartPosition.CenterParent
        Me.BackColor = Color.Black
        Me.ForeColor = Color.FromArgb(226, 232, 240)
        Me.Font = New Font("Segoe UI", 9)

        ' 必须先接上 Tick：漏了这一步窗口开出来就是静止的（游戏一直停在"暂停"态，
        ' 而且 --snake-window 自检永远等不到帧）。Tick 只在这里挂一次。
        AddHandler m_timer.Tick, AddressOf onTimerTick

        Dim width As Integer = Snake2.Game.ViewCols * Snake2.Game.CellSize
        Dim height As Integer = Snake2.Game.ViewRows * Snake2.Game.CellSize

        Me.ClientSize = New Size(width + 380, height + 64)

        m_gamePanel = New Panel With {
            .Location = New Point(0, 0),
            .Size = New Size(width, height),
            .BackColor = Color.Black
        }
        AddHandler m_gamePanel.Paint, AddressOf onGamePaint

        Dim side As New Panel With {
            .Location = New Point(width + 8, 0),
            .Size = New Size(368, height),
            .BackColor = Color.FromArgb(15, 22, 30)
        }

        m_status = New Label With {
            .Location = New Point(8, 8),
            .Size = New Size(352, 46),
            .ForeColor = Color.FromArgb(34, 211, 238),
            .Font = New Font("Consolas", 10, FontStyle.Bold)
        }

        Dim sensorLabel As New Label With {
            .Text = "感觉通道（大脑看到了什么）",
            .Location = New Point(8, 58),
            .Size = New Size(352, 20),
            .ForeColor = Color.FromArgb(148, 163, 184)
        }

        m_sensors = New ChannelBars With {
            .Location = New Point(8, 80),
            .Size = New Size(352, 220)
        }

        Dim motorLabel As New Label With {
            .Text = "运动读出（大脑命令往哪走）",
            .Location = New Point(8, 306),
            .Size = New Size(352, 20),
            .ForeColor = Color.FromArgb(148, 163, 184)
        }

        m_motors = New ChannelBars With {
            .Location = New Point(8, 328),
            .Size = New Size(352, 96)
        }

        m_detail = New Label With {
            .Location = New Point(8, 428),
            .Size = New Size(352, 62),
            .ForeColor = Color.FromArgb(203, 213, 225),
            .Font = New Font("Consolas", 8.5)
        }

        m_training = New Label With {
            .Location = New Point(8, 494),
            .Size = New Size(352, 60),
            .ForeColor = Color.FromArgb(250, 204, 21),
            .Font = New Font("Consolas", 8.5)
        }

        Call side.Controls.Add(m_status)
        Call side.Controls.Add(sensorLabel)
        Call side.Controls.Add(m_sensors)
        Call side.Controls.Add(motorLabel)
        Call side.Controls.Add(m_motors)
        Call side.Controls.Add(m_detail)
        Call side.Controls.Add(m_training)
        Call side.Controls.Add(createToolbar(side, height))

        Call Me.Controls.Add(m_gamePanel)
        Call Me.Controls.Add(side)
    End Sub

    Private Function createToolbar(parent As Panel, height As Integer) As Control
        Dim panel As New FlowLayoutPanel With {
            .Location = New Point(4, height - 74),
            .Size = New Size(360, 70),
            .WrapContents = True,
            .BackColor = Color.Transparent,
            .ForeColor = Me.ForeColor
        }

        m_playButton = New Button With {.Text = "暂停", .Width = 62, .Height = 26}
        Dim reset As New Button With {.Text = "新一局", .Width = 62, .Height = 26}
        Dim train As New Button With {.Text = "训练解码器", .Width = 96, .Height = 26}
        Dim loadTrained As New Button With {.Text = "载入已训练", .Width = 92, .Height = 26}

        AddHandler m_playButton.Click, Sub() togglePlay()
        AddHandler reset.Click, Sub() startNewEpisode()
        AddHandler train.Click, Sub() trainDecoder()
        AddHandler loadTrained.Click, Sub() loadTrainedDecoder()

        m_speedBox = New NumericUpDown With {.Minimum = 30, .Maximum = 1000, .Value = 120, .Width = 62}
        m_tickBox = New NumericUpDown With {.Minimum = 1, .Maximum = 20, .Value = 1, .Width = 46}

        AddHandler m_speedBox.ValueChanged, Sub() m_timer.Interval = CInt(m_speedBox.Value)

        Call panel.Controls.Add(m_playButton)
        Call panel.Controls.Add(reset)
        Call panel.Controls.Add(train)
        Call panel.Controls.Add(loadTrained)
        Call panel.Controls.Add(New Label With {.Text = "间隔(ms)", .AutoSize = True, .Margin = New Padding(6, 8, 2, 0)})
        Call panel.Controls.Add(m_speedBox)
        Call panel.Controls.Add(New Label With {.Text = "每帧步数", .AutoSize = True, .Margin = New Padding(6, 8, 2, 0)})
        Call panel.Controls.Add(m_tickBox)

        Return panel
    End Function

    Private Sub onGamePaint(sender As Object, e As PaintEventArgs)
        Dim g As Graphics = e.Graphics

        g.SmoothingMode = SmoothingMode.None
        g.Clear(Color.Black)

        If m_session IsNot Nothing Then
            Call m_session.Render.Draw(g)
        End If
    End Sub

#End Region

#Region "会话推进"

    Private Sub resetSession(decoder As SnakeDecoder, autoStart As Boolean)
        m_timer.Stop()

        If m_brain Is Nothing Then m_brain = m_playground.CreateBrain()
        If decoder Is Nothing Then
            decoder = New SnakeDecoder(m_brain.MotorFeatures.Length)
        End If

        m_session = m_playground.CreateSession(m_brain, decoder, Me)
        AddHandler m_session.Stepped, AddressOf onStepped

        Call m_session.NewEpisode()
        Call updateReadouts(m_session.LastStep)

        m_timer.Interval = CInt(m_speedBox.Value)

        If autoStart Then
            m_timer.Start()
            m_playButton.Text = "暂停"
        Else
            m_playButton.Text = "开始"
        End If
    End Sub

    ''' <summary>
    ''' 供命令行自检抓屏前调用：先停表，抓到的才是"恰好跑了 N 帧"的那一帧。
    ''' </summary>
    ''' <remarks>
    ''' 抓屏前的 <c>Application.DoEvents()</c> 会把定时器消息再抽进来（重入），
    ''' 不先停表的话游戏会在抓屏过程中继续往前跑，帧数与画面都对不上。
    ''' </remarks>
    Friend Sub PauseForProbe()
        m_timer.Stop()
        m_playButton.Text = "开始"
    End Sub

    ''' <summary>停过机之后不再重复停机。</summary>
    Private m_stopped As Boolean

    ''' <summary>
    ''' 关窗即停机：定时器是"游戏引擎 + 大脑逐步仿真"的唯一驱动，停掉它两者就都停了。
    ''' </summary>
    ''' <remarks>
    ''' 不停表的话后果很具体：<see cref="Timer"/> 的 WM_TIMER 发到的是它自己的隐藏窗口，
    ''' <b>不随窗体销毁而结束</b> —— 关窗之后会话继续 tick，主窗口的三维点云
    ''' 也就继续被脑活动点亮（看起来"关不掉地一直闪"）。
    ''' </remarks>
    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        Call shutdown()
        Call MyBase.OnFormClosed(e)
    End Sub

    ''' <remarks>
    ''' 兜底再停一次：即使窗体是被 Dispose 掉的（没走 Close），也不该留下一个
    ''' 还在后台推进游戏与仿真的定时器。重复调用由 <see cref="m_stopped"/> 挡住。
    ''' </remarks>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Call shutdown()

        If disposing Then
            m_timer.Dispose()
        End If

        Call MyBase.Dispose(disposing)
    End Sub

    Private Sub shutdown()
        If m_stopped Then Return

        m_stopped = True

        m_timer.Stop()

        If m_session IsNot Nothing Then
            RemoveHandler m_session.Stepped, AddressOf onStepped
            m_session = Nothing
        End If

        ' 大脑的显存要还回去：重开窗口会重新装配一份
        If m_brain IsNot Nothing Then
            Try
                Call m_brain.Dispose()
            Catch ex As Exception
                System.Diagnostics.Debug.WriteLine($"unable to release the game brain: {ex.Message}")
            End Try

            m_brain = Nothing
        End If
    End Sub

    Private Sub togglePlay()
        If m_timer.Enabled Then
            m_timer.Stop()
            m_playButton.Text = "开始"
        Else
            m_timer.Start()
            m_playButton.Text = "暂停"
        End If
    End Sub

    Private Sub startNewEpisode()
        If m_session Is Nothing Then Return

        Call m_session.NewEpisode()
        Call m_gamePanel.Invalidate()
    End Sub

    Private Sub stepOnce()
        If m_session Is Nothing Then Return

        For i As Integer = 1 To Math.Max(1, CInt(m_tickBox.Value))
            Call m_session.Tick()

            If m_session.GameOver Then
                Call m_session.NewEpisode()
                Exit For
            End If
        Next

        Call m_gamePanel.Invalidate()
    End Sub

    Private Sub onTimerTick(sender As Object, e As EventArgs)
        Call stepOnce()
    End Sub

    Private Sub onStepped(session As SnakeSession, frame As SnakeStep)
        ' 读数必须在这里刷新：resetSession 里那次调用拿到的是"还没跑过的空帧"，
        ' 只在开局刷一次的话两个条形读数框永远是空的（每 tick 的通知才是数据源）
        Call updateReadouts(frame)
        RaiseEvent BrainActivityChanged(frame.ActiveNeurons, frame)
    End Sub

    ''' <summary>刷新右侧读数（感觉通道 / 运动读出 / 统计）。</summary>
    Private Sub updateReadouts(frame As SnakeStep)
        If frame Is Nothing Then Return

        Dim names As New List(Of String)()
        Dim values As New List(Of Double)()

        For c As Integer = 0 To frame.Sensors.Length - 1
            Call names.Add(SnakeSensors.ChannelName(c))
            Call values.Add(frame.Sensors(c))
        Next

        ' 感觉通道的语义就是 [0,1] 的强度，量程固定；运动组是"本 tick 的脉冲个数"，
        ' 各组神经元数上百，固定量程 1 会让每一根都顶满（看似有数据其实读不出差别），
        ' 因此让它自适应（见 ChannelBars.SetData 的 maximum <= 0 分支）
        m_sensors.SetData(names.ToArray(), values.ToArray(), Color.FromArgb(34, 211, 238), 1.0)

        Dim moveNames As String() = {"上 ↑", "下 ↓", "左 ←", "右 →"}

        m_motors.SetData(moveNames, frame.MotorSpikes, Color.FromArgb(250, 204, 21))

        m_status.Text = $"得分 {frame.Score}  最高 {m_session.BestScore}  " &
                        $"方向 {describe(frame.Direction)}{Environment.NewLine}" &
                        $"步数 {m_session.Steps}  局数 {m_session.Episodes}"

        m_detail.Text = $"脑活动 {frame.ActiveNeurons.Length:N0} 个神经元 / tick（运动神经元 {m_brain.MotorActiveCount} 个）" &
                        Environment.NewLine &
                        $"距离食物 {(If(frame.FoodDistance < 0, "—", frame.FoodDistance.ToString))} 格  " &
                        $"单步路径 {m_brain.LastStepPath}" &
                        Environment.NewLine &
                        $"感觉输入 {m_brain.SensorNeurons(0).Length * SnakeSensors.ChannelCount:N0} 个 afferent 神经元，" &
                        $"读出 {m_brain.MotorFeatures.Length:N0} 个 efferent 神经元"
    End Sub

    Private Shared Function describe(direction As Point) As String
        If direction.X > 0 Then Return "→"
        If direction.X < 0 Then Return "←"
        If direction.Y > 0 Then Return "↓"

        Return "↑"
    End Function

#End Region

#Region "训练"

    ''' <summary>在后台训练解码器，完成后立刻换上。</summary>
    Private Sub trainDecoder()
        ' 样本量是这条"脑机接口"读出层的瓶颈：1500 条样本要拟合 1488 × 4 个权重，
        ' 前一轮 6 局 × 250 tick 的准确率只有 0.70，蛇看起来还是乱走；
        ' 这里把样本量翻一倍（8 局 × 350 tick）
        Dim episodes As Integer = 8
        Dim ticks As Integer = 350

        m_training.Text = "正在训练解码器（后台）..."

        Call Task.Run(
            Function() As SnakeTrainingReport
                Return m_playground.Train(episodes, ticks, Sub(message) reportTraining(message))
            End Function) _
            .ContinueWith(
                Sub(task As Task(Of SnakeTrainingReport))
                    If task.IsFaulted Then
                        m_training.Text = $"训练失败: {task.Exception?.GetBaseException()?.Message}"

                        Return
                    End If

                    Dim training As SnakeTrainingReport = task.Result

                    If training.TrainedDecoder IsNot Nothing AndAlso m_session IsNot Nothing Then
                        m_session.Decoder = training.TrainedDecoder
                    End If

                    m_training.Text = training.Describe()
                    Call saveTrained(training.TrainedDecoder)
                End Sub,
                TaskScheduler.FromCurrentSynchronizationContext())
    End Sub

    Private Sub reportTraining(message As String)
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return

        Call BeginInvoke(New Action(Sub() m_training.Text = message))
    End Sub

    Private Sub saveTrained(decoder As SnakeDecoder)
        If decoder Is Nothing Then Return

        Try
            Dim dir As String = Path.Combine(m_playground.DataDir, "snn-output", "snake")

            Call Directory.CreateDirectory(dir)
            Call decoder.Save(Path.Combine(dir, "snake_decoder.csv"))
        Catch ex As Exception
            m_training.Text &= $"（保存失败: {ex.Message}）"
        End Try
    End Sub

    Private Sub loadTrainedDecoder()
        Try
            ' 局部变量避开 file / path：会遮蔽 System.IO.File / Path（VB 不区分大小写）
            Dim decoderFile As String = IO.Path.Combine(m_playground.DataDir, "snn-output", "snake", "snake_decoder.csv")

            If Not IO.File.Exists(decoderFile) Then
                m_training.Text = $"没有找到已训练的解码器: {decoderFile}"

                Return
            End If

            Dim decoder As SnakeDecoder = SnakeDecoder.Load(decoderFile, m_brain.MotorFeatures.Length)

            m_session.Decoder = decoder
            m_training.Text = $"已载入已训练的解码器（{decoderFile}）"
        Catch ex As Exception
            m_training.Text = $"载入失败: {ex.Message}"
        End Try
    End Sub

#End Region

End Class

''' <summary>
''' 竖排条形读数控件：一行一个通道，画名称 + 条形 + 数值。
''' </summary>
''' <remarks>
''' 手写而不是用 ProgressBar：一次要显示 16 行，ProgressBar 无法紧凑排布，
''' 也没法在同一行里带标签。
''' </remarks>
Public Class ChannelBars
    Inherits Control

    Private m_names As String() = New String() {}
    Private m_values As Double() = New Double() {}
    Private m_color As Color = Color.Cyan
    Private m_maximum As Double = 1.0

    ''' <summary>量程是否是自适应（<paramref name="maximum"/> 传 &lt;= 0 时开启）。</summary>
    Private m_adaptive As Boolean

    ''' <summary>自适应量程的回落速度：每帧只允许缩到这个比例，避免读数一起一伏地跳。</summary>
    Private Const AdaptiveDecay As Double = 0.97

    ''' <summary>
    ''' 灌入一行行读数。
    ''' </summary>
    ''' <param name="maximum">
    ''' 量程。传 &lt;= 0 表示自适应：峰值立刻抬到最高，之后按 <see cref="AdaptiveDecay"/> 缓慢回落。
    ''' 适合"数量级事先不知道"的读数（例如每 tick 的脉冲个数）。
    ''' </param>
    Public Sub SetData(names As String(), values As Double(), color As Color, Optional maximum As Double = 0)
        m_names = If(names, New String() {})
        m_values = If(values, New Double() {})
        m_color = color
        m_adaptive = maximum <= 0

        If m_adaptive Then
            Dim peak As Double = 0

            For i As Integer = 0 To m_values.Length - 1
                Dim magnitude As Double = Math.Abs(m_values(i))

                If magnitude > peak Then peak = magnitude
            Next

            ' 峰值立即跟上（否则强读数会顶格看不出差别），之后缓慢回落
            m_maximum = If(peak >= m_maximum, peak, m_maximum * AdaptiveDecay)

            If m_maximum <= 0 Then m_maximum = 1.0
        Else
            m_maximum = maximum
        End If

        Call Invalidate()
    End Sub

    Public Sub New()
        Me.DoubleBuffered = True
        Me.BackColor = Color.FromArgb(20, 28, 38)
        Me.ForeColor = Color.FromArgb(203, 213, 225)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        Dim rowHeight As Single = Math.Max(12.0F, Me.ClientSize.Height / Math.Max(1, m_names.Length))

        Using font As New Font("Consolas", 8.0F)
            For i As Integer = 0 To m_names.Length - 1
                Dim y As Single = i * rowHeight
                Dim value As Double = If(i < m_values.Length, m_values(i), 0)
                Dim ratio As Double = Math.Min(1.0, Math.Abs(value) / m_maximum)
                Dim labelWidth As Single = 96

                Using brush As New SolidBrush(Me.ForeColor)
                    Call g.DrawString(m_names(i), font, brush, 2, y + 1)
                End Using

                Using brush As New SolidBrush(m_color)
                    Call g.FillRectangle(brush, labelWidth, y + 2, CSng((Me.ClientSize.Width - labelWidth - 40) * ratio),
                                         Math.Max(4.0F, rowHeight - 5))
                End Using

                Using brush As New SolidBrush(Me.ForeColor)
                    ' 自适应量程下的读数是"个脉冲"，F2 那串小数会画不下
                    Dim text As String = If(m_adaptive, value.ToString("F0"), value.ToString("F2"))

                    Call g.DrawString(text, font, brush, Me.ClientSize.Width - 38, y + 1)
                End Using
            Next
        End Using
    End Sub
End Class
