
' SnakeBrainForm 的界面布局（Windows 窗体设计器维护的声明式代码）。
' 这里只放"控件怎么摆"的代码：控件实例化、属性赋值、容器装配与事件声明；
' 会话推进、大脑仿真与解码器训练等逻辑仍然留在 SnakeBrainForm.vb 里。
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class SnakeBrainForm
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        ' 兜底再停一次：即使窗体是被 Dispose 掉的（没走 Close），也不该留下一个
        ' 还在后台推进游戏与仿真的定时器（重复调用由 SnakeBrainForm 的 m_stopped 挡住）。
        ' 停机必须排在 components.Dispose() 之前：定时器一旦被释放，
        ' shutdown 里的 Stop 就落在已释放对象上了。
        Call shutdown()

        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            Call MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()

        components = New System.ComponentModel.Container()

        SuspendLayout()
        ' 
        ' SnakeBrainForm
        ' 
        Text = "果蝇大脑玩贪吃蛇 — 实时观战"
        StartPosition = FormStartPosition.CenterParent
        BackColor = Color.Black
        ForeColor = Color.FromArgb(226, 232, 240)
        Font = New Font("Segoe UI", 9)

        ' 两半屏都是绝对坐标摆位，坐标全部按游戏本体的可视区算好写成常数字面量：
        '   左半屏 = Snake2 可视区 40 列 × 30 行 × 20 px/格 = 800 × 600
        '   右半屏 = 固定宽 368，与左半屏之间留 8 px 间隔
        '   客户区 = 800 + 380 宽、600 + 64 高（底部留白）
        ClientSize = New Size(1180, 664)
        ' 
        ' m_gamePanel：左半屏 —— 游戏画面（Paint 事件里直接调 Snake2 的 Render.Draw）
        ' 
        m_gamePanel.Location = New Point(0, 0)
        m_gamePanel.Size = New Size(800, 600)
        m_gamePanel.BackColor = Color.Black
        ' AddHandler m_gamePanel.Paint, AddressOf onGamePaint
        ' 
        ' side：右半屏 —— 大脑此刻在做什么（读数 + 统计 + 工具栏）
        ' 
        side.Location = New Point(808, 0)
        side.Size = New Size(368, 600)
        side.BackColor = Color.FromArgb(15, 22, 30)
        ' 
        ' m_timer：会话推进的唯一驱动（游戏与大脑逐步仿真都靠它）
        ' 
        m_timer = New Timer(components)
        ' Tick 只在这里挂一次：漏了这一步窗口开出来就是静止的（游戏一直停在"暂停"态，
        ' 而且 --snake-window 自检永远等不到帧）
        ' AddHandler m_timer.Tick, AddressOf onTimerTick
        ' 
        ' m_status：得分 / 最高分 / 方向 / 步数
        ' 
        m_status = New Label()
        m_status.Location = New Point(8, 8)
        m_status.Size = New Size(352, 46)
        m_status.ForeColor = Color.FromArgb(34, 211, 238)
        m_status.Font = New Font("Consolas", 10, FontStyle.Bold)

        sensorLabel.Text = "感觉通道（大脑看到了什么）"
        sensorLabel.Location = New Point(8, 58)
        sensorLabel.Size = New Size(352, 20)
        sensorLabel.ForeColor = Color.FromArgb(148, 163, 184)
        ' 
        ' m_sensors：16 个感觉通道的强度条
        ' 
        m_sensors = New ChannelBars()
        m_sensors.Location = New Point(8, 80)
        m_sensors.Size = New Size(352, 220)

        motorLabel.Text = "运动读出（大脑命令往哪走）"
        motorLabel.Location = New Point(8, 306)
        motorLabel.Size = New Size(352, 20)
        motorLabel.ForeColor = Color.FromArgb(148, 163, 184)
        ' 
        ' m_motors：4 组运动神经元的脉冲数
        ' 
        m_motors = New ChannelBars()
        m_motors.Location = New Point(8, 328)
        m_motors.Size = New Size(352, 96)

        m_detail = New Label()
        m_detail.Location = New Point(8, 428)
        m_detail.Size = New Size(352, 62)
        m_detail.ForeColor = Color.FromArgb(203, 213, 225)
        m_detail.Font = New Font("Consolas", 8.5)

        m_training = New Label()
        m_training.Location = New Point(8, 494)
        m_training.Size = New Size(352, 60)
        m_training.ForeColor = Color.FromArgb(250, 204, 21)
        m_training.Font = New Font("Consolas", 8.5)
        ' 
        ' toolbar：右下角 —— 播放 / 新一局 / 训练 / 载入 + 速度与每帧步数
        ' 
        toolbar.Location = New Point(4, 526)
        toolbar.Size = New Size(360, 70)
        toolbar.WrapContents = True
        toolbar.BackColor = Color.Transparent
        toolbar.ForeColor = ForeColor

        m_playButton = New Button()
        m_playButton.Text = "暂停"
        m_playButton.Width = 62
        m_playButton.Height = 26
        ' AddHandler m_playButton.Click, AddressOf togglePlay

        m_resetButton.Text = "新一局"
        m_resetButton.Width = 62
        m_resetButton.Height = 26
        ' AddHandler m_resetButton.Click, AddressOf startNewEpisode

        m_trainButton.Text = "训练解码器"
        m_trainButton.Width = 96
        m_trainButton.Height = 26
        ' AddHandler m_trainButton.Click, AddressOf trainDecoder

        m_loadButton.Text = "载入已训练"
        m_loadButton.Width = 92
        m_loadButton.Height = 26
        ' AddHandler m_loadButton.Click, AddressOf loadTrainedDecoder

        speedLabel.Text = "间隔(ms)"
        speedLabel.AutoSize = True
        speedLabel.Margin = New Padding(6, 8, 2, 0)

        m_speedBox = New NumericUpDown()
        m_speedBox.Minimum = 30
        m_speedBox.Maximum = 1000
        m_speedBox.Value = 120
        m_speedBox.Width = 62
        ' AddHandler m_speedBox.ValueChanged, AddressOf onSpeedChanged

        tickLabel.Text = "每帧步数"
        tickLabel.AutoSize = True
        tickLabel.Margin = New Padding(6, 8, 2, 0)

        m_tickBox = New NumericUpDown()
        m_tickBox.Minimum = 1
        m_tickBox.Maximum = 20
        m_tickBox.Value = 1
        m_tickBox.Width = 46

        toolbar.Controls.Add(m_playButton)
        toolbar.Controls.Add(m_resetButton)
        toolbar.Controls.Add(m_trainButton)
        toolbar.Controls.Add(m_loadButton)
        toolbar.Controls.Add(speedLabel)
        toolbar.Controls.Add(m_speedBox)
        toolbar.Controls.Add(tickLabel)
        toolbar.Controls.Add(m_tickBox)

        side.Controls.Add(m_status)
        side.Controls.Add(sensorLabel)
        side.Controls.Add(m_sensors)
        side.Controls.Add(motorLabel)
        side.Controls.Add(m_motors)
        side.Controls.Add(m_detail)
        side.Controls.Add(m_training)
        side.Controls.Add(toolbar)

        Controls.Add(m_gamePanel)
        Controls.Add(side)

        ResumeLayout(False)
        PerformLayout()
    End Sub

    ' ---- 只负责摆位的控件（声明即建好，属性在 InitializeComponent 里赋值）----
    Dim WithEvents m_gamePanel As New Panel()
    Dim WithEvents side As New Panel()
    Dim WithEvents sensorLabel As New Label()
    Dim WithEvents motorLabel As New Label()
    Dim WithEvents toolbar As New FlowLayoutPanel()
    Dim WithEvents speedLabel As New Label()
    Dim WithEvents tickLabel As New Label()
    Dim WithEvents m_resetButton As New Button()
    Dim WithEvents m_trainButton As New Button()
    Dim WithEvents m_loadButton As New Button()

    Private WithEvents m_sensors As ChannelBars
    Private WithEvents m_motors As ChannelBars
    Private WithEvents m_status As Label
    Private WithEvents m_detail As Label
    Private WithEvents m_training As Label
    Private WithEvents m_playButton As Button
    Private WithEvents m_speedBox As NumericUpDown
    Private WithEvents m_tickBox As NumericUpDown
    Private WithEvents m_timer As Timer
End Class
