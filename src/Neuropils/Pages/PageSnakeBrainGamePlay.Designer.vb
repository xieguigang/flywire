
' SnakeBrainForm 的界面布局（Windows 窗体设计器维护的声明式代码）。
' 这里只放"控件怎么摆"的代码：控件实例化、属性赋值、容器装配与事件声明；
' 会话推进、大脑仿真与解码器训练等逻辑仍然留在 SnakeBrainForm.vb 里。
Imports Galaxy.Workbench.DockDocument

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class PageSnakeBrainGamePlay
    Inherits DocumentWindow

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
        components = New ComponentModel.Container()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(PageSnakeBrainGamePlay))
        m_gamePanel = New Panel()
        side = New Panel()
        m_status = New Label()
        sensorLabel = New Label()
        m_sensors = New ChannelBars()
        motorLabel = New Label()
        m_motors = New ChannelBars()
        m_detail = New Label()
        m_training = New Label()
        toolbar = New FlowLayoutPanel()
        m_playButton = New Button()
        m_resetButton = New Button()
        m_trainButton = New Button()
        m_loadButton = New Button()
        speedLabel = New Label()
        m_speedBox = New NumericUpDown()
        tickLabel = New Label()
        m_tickBox = New NumericUpDown()
        m_timer = New Timer(components)
        SplitContainer1 = New SplitContainer()
        side.SuspendLayout()
        toolbar.SuspendLayout()
        CType(m_speedBox, ComponentModel.ISupportInitialize).BeginInit()
        CType(m_tickBox, ComponentModel.ISupportInitialize).BeginInit()
        CType(SplitContainer1, ComponentModel.ISupportInitialize).BeginInit()
        SplitContainer1.Panel1.SuspendLayout()
        SplitContainer1.Panel2.SuspendLayout()
        SplitContainer1.SuspendLayout()
        SuspendLayout()
        ' 
        ' m_gamePanel
        ' 
        m_gamePanel.BackColor = Color.Black
        m_gamePanel.Dock = DockStyle.Fill
        m_gamePanel.Location = New Point(0, 0)
        m_gamePanel.MinimumSize = New Size(807, 663)
        m_gamePanel.Name = "m_gamePanel"
        m_gamePanel.Size = New Size(807, 663)
        m_gamePanel.TabIndex = 0
        ' 
        ' side
        ' 
        side.BackColor = Color.FromArgb(CByte(15), CByte(22), CByte(30))
        side.Controls.Add(m_status)
        side.Controls.Add(sensorLabel)
        side.Controls.Add(m_sensors)
        side.Controls.Add(motorLabel)
        side.Controls.Add(m_motors)
        side.Controls.Add(m_detail)
        side.Controls.Add(m_training)
        side.Controls.Add(toolbar)
        side.Dock = DockStyle.Fill
        side.Location = New Point(0, 0)
        side.MinimumSize = New Size(370, 663)
        side.Name = "side"
        side.Size = New Size(370, 663)
        side.TabIndex = 1
        ' 
        ' m_status
        ' 
        m_status.Font = New Font("Consolas", 10.0F, FontStyle.Bold)
        m_status.ForeColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        m_status.Location = New Point(8, 8)
        m_status.Name = "m_status"
        m_status.Size = New Size(352, 46)
        m_status.TabIndex = 0
        ' 
        ' sensorLabel
        ' 
        sensorLabel.ForeColor = Color.FromArgb(CByte(148), CByte(163), CByte(184))
        sensorLabel.Location = New Point(8, 58)
        sensorLabel.Name = "sensorLabel"
        sensorLabel.Size = New Size(352, 20)
        sensorLabel.TabIndex = 1
        sensorLabel.Text = "感觉通道（大脑看到了什么）"
        ' 
        ' m_sensors
        ' 
        m_sensors.BackColor = Color.FromArgb(CByte(20), CByte(28), CByte(38))
        m_sensors.ForeColor = Color.FromArgb(CByte(203), CByte(213), CByte(225))
        m_sensors.Location = New Point(8, 80)
        m_sensors.Name = "m_sensors"
        m_sensors.Size = New Size(352, 220)
        m_sensors.TabIndex = 2
        ' 
        ' motorLabel
        ' 
        motorLabel.ForeColor = Color.FromArgb(CByte(148), CByte(163), CByte(184))
        motorLabel.Location = New Point(8, 306)
        motorLabel.Name = "motorLabel"
        motorLabel.Size = New Size(352, 20)
        motorLabel.TabIndex = 3
        motorLabel.Text = "运动读出（大脑命令往哪走）"
        ' 
        ' m_motors
        ' 
        m_motors.BackColor = Color.FromArgb(CByte(20), CByte(28), CByte(38))
        m_motors.ForeColor = Color.FromArgb(CByte(203), CByte(213), CByte(225))
        m_motors.Location = New Point(8, 328)
        m_motors.Name = "m_motors"
        m_motors.Size = New Size(352, 96)
        m_motors.TabIndex = 4
        ' 
        ' m_detail
        ' 
        m_detail.Font = New Font("Consolas", 8.5F)
        m_detail.ForeColor = Color.FromArgb(CByte(203), CByte(213), CByte(225))
        m_detail.Location = New Point(8, 437)
        m_detail.Name = "m_detail"
        m_detail.Size = New Size(352, 62)
        m_detail.TabIndex = 5
        ' 
        ' m_training
        ' 
        m_training.Font = New Font("Consolas", 8.5F)
        m_training.ForeColor = Color.FromArgb(CByte(250), CByte(204), CByte(21))
        m_training.Location = New Point(8, 520)
        m_training.Name = "m_training"
        m_training.Size = New Size(352, 60)
        m_training.TabIndex = 6
        ' 
        ' toolbar
        ' 
        toolbar.BackColor = Color.Transparent
        toolbar.Controls.Add(m_playButton)
        toolbar.Controls.Add(m_resetButton)
        toolbar.Controls.Add(m_trainButton)
        toolbar.Controls.Add(m_loadButton)
        toolbar.Controls.Add(speedLabel)
        toolbar.Controls.Add(m_speedBox)
        toolbar.Controls.Add(tickLabel)
        toolbar.Controls.Add(m_tickBox)
        toolbar.ForeColor = SystemColors.ControlText
        toolbar.Location = New Point(3, 583)
        toolbar.Name = "toolbar"
        toolbar.Size = New Size(360, 70)
        toolbar.TabIndex = 7
        ' 
        ' m_playButton
        ' 
        m_playButton.Location = New Point(3, 3)
        m_playButton.Name = "m_playButton"
        m_playButton.Size = New Size(62, 26)
        m_playButton.TabIndex = 0
        m_playButton.Text = "暂停"
        ' 
        ' m_resetButton
        ' 
        m_resetButton.Location = New Point(71, 3)
        m_resetButton.Name = "m_resetButton"
        m_resetButton.Size = New Size(62, 26)
        m_resetButton.TabIndex = 1
        m_resetButton.Text = "新一局"
        ' 
        ' m_trainButton
        ' 
        m_trainButton.Location = New Point(139, 3)
        m_trainButton.Name = "m_trainButton"
        m_trainButton.Size = New Size(96, 26)
        m_trainButton.TabIndex = 2
        m_trainButton.Text = "训练解码器"
        ' 
        ' m_loadButton
        ' 
        m_loadButton.Location = New Point(241, 3)
        m_loadButton.Name = "m_loadButton"
        m_loadButton.Size = New Size(92, 26)
        m_loadButton.TabIndex = 3
        m_loadButton.Text = "载入已训练"
        ' 
        ' speedLabel
        ' 
        speedLabel.AutoSize = True
        speedLabel.ForeColor = Color.Cyan
        speedLabel.Location = New Point(6, 40)
        speedLabel.Margin = New Padding(6, 8, 2, 0)
        speedLabel.Name = "speedLabel"
        speedLabel.Size = New Size(57, 15)
        speedLabel.TabIndex = 4
        speedLabel.Text = "间隔(ms)"
        ' 
        ' m_speedBox
        ' 
        m_speedBox.Location = New Point(68, 35)
        m_speedBox.Maximum = New Decimal(New Integer() {1000, 0, 0, 0})
        m_speedBox.Minimum = New Decimal(New Integer() {30, 0, 0, 0})
        m_speedBox.Name = "m_speedBox"
        m_speedBox.Size = New Size(62, 23)
        m_speedBox.TabIndex = 5
        m_speedBox.Value = New Decimal(New Integer() {120, 0, 0, 0})
        ' 
        ' tickLabel
        ' 
        tickLabel.AutoSize = True
        tickLabel.ForeColor = Color.Cyan
        tickLabel.Location = New Point(139, 40)
        tickLabel.Margin = New Padding(6, 8, 2, 0)
        tickLabel.Name = "tickLabel"
        tickLabel.Size = New Size(59, 15)
        tickLabel.TabIndex = 6
        tickLabel.Text = "每帧步数"
        ' 
        ' m_tickBox
        ' 
        m_tickBox.Location = New Point(203, 35)
        m_tickBox.Maximum = New Decimal(New Integer() {20, 0, 0, 0})
        m_tickBox.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        m_tickBox.Name = "m_tickBox"
        m_tickBox.Size = New Size(46, 23)
        m_tickBox.TabIndex = 7
        m_tickBox.Value = New Decimal(New Integer() {1, 0, 0, 0})
        ' 
        ' m_timer
        ' 
        ' 
        ' SplitContainer1
        ' 
        SplitContainer1.Dock = DockStyle.Fill
        SplitContainer1.FixedPanel = FixedPanel.Panel2
        SplitContainer1.IsSplitterFixed = True
        SplitContainer1.Location = New Point(0, 0)
        SplitContainer1.MinimumSize = New Size(1181, 663)
        SplitContainer1.Name = "SplitContainer1"
        ' 
        ' SplitContainer1.Panel1
        ' 
        SplitContainer1.Panel1.Controls.Add(m_gamePanel)
        ' 
        ' SplitContainer1.Panel2
        ' 
        SplitContainer1.Panel2.Controls.Add(side)
        SplitContainer1.Panel2MinSize = 370
        SplitContainer1.Size = New Size(1181, 663)
        SplitContainer1.SplitterDistance = 807
        SplitContainer1.TabIndex = 2
        ' 
        ' PageSnakeBrainGamePlay
        ' 
        ' AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        BackColor = Color.Black
        ClientSize = New Size(1181, 663)
        Controls.Add(SplitContainer1)
        ' DockState = Microsoft.VisualStudio.WinForms.Docking.DockState.Float
        '  DockAreas = Microsoft.VisualStudio.WinForms.Docking.DockAreas.Float Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockLeft Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockRight Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockTop Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockBottom Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.Document
        '  DoubleBuffered = True
        ' Font = New Font("Segoe UI", 9.0F)
        ForeColor = Color.FromArgb(CByte(226), CByte(232), CByte(240))
        Icon = CType(resources.GetObject("$this.Icon"), Icon)
        Name = "PageSnakeBrainGamePlay"
        '  ShowHint = Microsoft.VisualStudio.WinForms.Docking.DockState.Unknown
        StartPosition = FormStartPosition.CenterParent
        TabPageContextMenuStrip = DockContextMenuStrip1
        Text = "果蝇大脑玩贪吃蛇 — 实时观战"
        side.ResumeLayout(False)
        toolbar.ResumeLayout(False)
        toolbar.PerformLayout()
        CType(m_speedBox, ComponentModel.ISupportInitialize).EndInit()
        CType(m_tickBox, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer1.Panel1.ResumeLayout(False)
        SplitContainer1.Panel2.ResumeLayout(False)
        CType(SplitContainer1, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer1.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    ' ---- 只负责摆位的控件（声明即建好，属性在 InitializeComponent 里赋值）----
    Dim WithEvents m_gamePanel As Panel
    Dim WithEvents side As Panel
    Dim WithEvents sensorLabel As Label
    Dim WithEvents motorLabel As Label
    Dim WithEvents toolbar As FlowLayoutPanel
    Dim WithEvents speedLabel As Label
    Dim WithEvents tickLabel As Label
    Dim WithEvents m_resetButton As Button
    Dim WithEvents m_trainButton As Button
    Dim WithEvents m_loadButton As Button

    Private WithEvents m_sensors As ChannelBars
    Private WithEvents m_motors As ChannelBars
    Private WithEvents m_status As Label
    Private WithEvents m_detail As Label
    Private WithEvents m_training As Label
    Private WithEvents m_playButton As Button
    Private WithEvents m_speedBox As NumericUpDown
    Private WithEvents m_tickBox As NumericUpDown
    Private WithEvents m_timer As Timer
    Friend WithEvents SplitContainer1 As SplitContainer
End Class
