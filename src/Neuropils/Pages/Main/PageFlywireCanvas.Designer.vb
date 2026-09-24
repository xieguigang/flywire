Imports Galaxy.Workbench.DockDocument
Imports Microsoft.VisualBasic.Drawing.DirectX
Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class PageFlywireCanvas
    Inherits DocumentWindow

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer


    ' ---- 工具条（声明式布局，参照 ResponseChartForm.Designer.vb 的模式）----
    ' 被其它类 (StimulationExperiment) 通过 FormMain 实例访问的保持 Friend；
    ' 事件源用 WithEvents 以便 Handles 绑定，替代原 createToolbar 里的 AddHandler。
    Dim m_toolStrip As ToolStrip
    Private WithEvents m_dimensionBox As ToolStripComboBox
    Private WithEvents m_connectionBox As ToolStripComboBox
    Private WithEvents m_lineColorBox As ToolStripComboBox
    Private WithEvents m_renderModeBox As ToolStripComboBox

    Friend m_holdLabel As ToolStripLabel

    ' ---- 主界面布局（声明式，参照 ResponseChartForm.Designer.vb 的模式）----
    ' 事件源用 WithEvents 以便 Handles 绑定，替代原 initializeUi / createSidebar / createReplayPanel 里的 AddHandler。
    Friend WithEvents m_canvas As DxScene3DCanvas
    Private m_split As SplitContainer
    Private WithEvents m_legend As CheckedListBox
    Private m_gradient As PictureBox
    Private m_details As TextBox
    Friend m_replayPanel As TableLayoutPanel
    Friend WithEvents m_replayFirst As Button
    Friend WithEvents m_replayPrev As Button
    Friend WithEvents m_replayPlay As Button
    Friend WithEvents m_replayNext As Button
    Friend WithEvents m_replayLast As Button
    Friend WithEvents m_replayClear As Button
    Friend WithEvents m_replayTrack As TrackBar
    Friend WithEvents m_replaySpeed As NumericUpDown
    Friend m_replayText As Label

    Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub

    Dim replayBottomPanel As TableLayoutPanel
    Dim sidebarPanel As TableLayoutPanel
    Dim replayButtonsPanel As FlowLayoutPanel

    Dim vlabel As Label
    Dim replayLabel As Label
    Dim infoLabel As Label
    Dim legendLabel As Label

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        SuspendLayout()
        ' 
        ' FormMain
        ' 
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1411, 818)
        Name = "FormMain"
        Text = "Neuropils"

        vlabel = New Label

        replayBottomPanel = New TableLayoutPanel
        sidebarPanel = New TableLayoutPanel
        replayButtonsPanel = New FlowLayoutPanel
        '' m_menuStrip
        '' 顶部菜单栏：停靠在窗体顶端，宽度取窗体客户区宽度 (1500, 见 initializeUi 的 ClientSize)，高度 24 为单排菜单的标准高度
        '' 
        'm_menuStrip = New MenuStrip()
        'm_menuStrip.Dock = DockStyle.Top
        'm_menuStrip.Location = New Point(0, 0)
        'm_menuStrip.Name = "m_menuStrip"
        'm_menuStrip.Size = New Size(1500, 24)
        'm_menuStrip.TabIndex = 0
        'm_menuStrip.Text = ""
        '' 
        '' m_fileMenu
        '' 
        'm_fileMenu = New ToolStripMenuItem()
        'm_fileMenu.Name = "m_fileMenu"
        'm_fileMenu.Size = New Size(61, 21)
        'm_fileMenu.Text = "文件 (&F)"
        '' 
        '' m_openItem
        '' 
        'm_openItem = New ToolStripMenuItem()
        'm_openItem.Name = "m_openItem"
        'm_openItem.Size = New Size(180, 22)
        'm_openItem.Text = "打开数据目录 (&O)..."
        '' 
        '' m_snapshotItem
        '' 
        'm_snapshotItem = New ToolStripMenuItem()
        'm_snapshotItem.Name = "m_snapshotItem"
        'm_snapshotItem.Size = New Size(168, 22)
        'm_snapshotItem.Text = "保存截图 (&S)..."
        '' 
        '' m_separator
        '' 
        'm_separator = New ToolStripSeparator()
        'm_separator.Name = "m_separator"
        'm_separator.Size = New Size(177, 6)
        ' 
        ' m_exitItem
        ' 
        'm_exitItem = New ToolStripMenuItem()
        'm_exitItem.Name = "m_exitItem"
        'm_exitItem.Size = New Size(120, 22)
        'm_exitItem.Text = "退出 (&X)"
        ' 
        ' m_viewMenu
        ' 
        'm_viewMenu = New ToolStripMenuItem()
        'm_viewMenu.Name = "m_viewMenu"
        'm_viewMenu.Size = New Size(60, 21)
        'm_viewMenu.Text = "视图 (&V)"
        ' 
        ' m_resetItem
        ' 
        'm_resetItem = New ToolStripMenuItem()
        'm_resetItem.Name = "m_resetItem"
        'm_resetItem.Size = New Size(140, 22)
        'm_resetItem.Text = "重置视角 (&R)"
        ' 
        ' m_helpMenu
        ' 
        'm_helpMenu = New ToolStripMenuItem()
        'm_helpMenu.Name = "m_helpMenu"
        'm_helpMenu.Size = New Size(60, 21)
        'm_helpMenu.Text = "帮助 (&H)"
        '' 
        '' m_menuStrip 容器装配
        '' 
        'm_menuStrip.SuspendLayout()
        'm_fileMenu.DropDownItems.AddRange(New ToolStripItem() {m_openItem, m_snapshotItem, m_separator})
        '' m_viewMenu.DropDownItems.Add(m_resetItem)
        'm_menuStrip.Items.AddRange(New ToolStripItem() {m_fileMenu, m_viewMenu, m_helpMenu})
        'm_menuStrip.ResumeLayout(False)
        ' 
        ' m_statusStrip
        ' 状态栏：停靠在窗体底端；宽度取窗体客户区宽度 (1500, 见 initializeUi 的 ClientSize)，
        ' 高度 22 为状态栏标准高度，故 y = 900 - 22 = 878
        ' 
        'm_statusStrip = New StatusStrip()
        'm_statusStrip.Dock = DockStyle.Bottom
        'm_statusStrip.Location = New Point(0, 878)
        'm_statusStrip.Name = "m_statusStrip"
        'm_statusStrip.Size = New Size(1500, 22)
        'm_statusStrip.TabIndex = 1
        '' 
        '' m_statusText
        '' 
        'm_statusText = New ToolStripStatusLabel()
        'm_statusText.Name = "m_statusText"
        'm_statusText.Size = New Size(100, 17)
        'm_statusText.Spring = True
        'm_statusText.Text = "就绪"
        'm_statusText.TextAlign = ContentAlignment.MiddleLeft
        '' 
        '' m_sceneText
        '' 
        'm_sceneText = New ToolStripStatusLabel()
        'm_sceneText.Name = "m_sceneText"
        'm_sceneText.Size = New Size(100, 17)
        'm_sceneText.Text = ""
        'm_sceneText.BorderSides = ToolStripStatusLabelBorderSides.Left
        '' 
        '' m_progress
        '' 
        'm_progress = New ToolStripProgressBar()
        'm_progress.Name = "m_progress"
        'm_progress.Size = New Size(220, 16)
        'm_progress.Visible = False
        'm_progress.Style = ProgressBarStyle.Marquee
        ' 
        ' m_chartLink
        ' 
        'm_chartLink = New LinkLabel()
        'm_chartLink.Name = "m_chartLink"
        'm_chartLink.AutoSize = True
        'm_chartLink.Enabled = False
        'm_chartLink.Size = New Size(56, 17)
        'm_chartLink.Text = "响应曲线"
        'm_chartLink.LinkBehavior = LinkBehavior.HoverUnderline
        'm_chartLink.LinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        'm_chartLink.ActiveLinkColor = Color.White
        'm_chartLink.VisitedLinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        'm_chartLink.DisabledLinkColor = Color.FromArgb(CByte(100), CByte(116), CByte(139))
        'm_chartLink.Margin = New Padding(6, 4, 6, 0)
        ' 
        ' m_snakeLink
        ' 
        'm_snakeLink = New LinkLabel()
        'm_snakeLink.Name = "m_snakeLink"
        'm_snakeLink.AutoSize = True
        'm_snakeLink.Size = New Size(140, 17)
        'm_snakeLink.Text = "果蝇大脑玩贪吃蛇"
        'm_snakeLink.LinkBehavior = LinkBehavior.HoverUnderline
        'm_snakeLink.LinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        'm_snakeLink.ActiveLinkColor = Color.White
        'm_snakeLink.VisitedLinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        'm_snakeLink.Margin = New Padding(6, 4, 6, 0)
        ' 
        ' m_statusStrip 容器装配
        ' 
        'm_statusStrip.SuspendLayout()
        'm_statusStrip.Items.Add(m_statusText)
        'm_statusStrip.Items.Add(m_sceneText)
        'm_statusStrip.Items.Add(m_progress)
        '' m_statusStrip.Items.Add(New ToolStripControlHost(m_chartLink) With {.Alignment = ToolStripItemAlignment.Left})
        '' m_statusStrip.Items.Add(New ToolStripControlHost(m_snakeLink) With {.Alignment = ToolStripItemAlignment.Left})
        'm_statusStrip.ResumeLayout(False)
        ' 
        ' FormMain (窗体自身)
        ' 
        Me.Text = "Neuropils - Drosophila brain 3D viewer"
        Me.ClientSize = New Size(1500, 900)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.MinimumSize = New Size(900, 600)
        ' 
        ' m_canvas
        ' 
        m_canvas = New DxScene3DCanvas()
        m_canvas.Dock = DockStyle.Fill
        m_canvas.AutoClear = False
        m_canvas.BackColor = Color.Black
        m_canvas.BackgroundColor = Color.FromArgb(12, 12, 18)
        m_canvas.Renderer = m_renderer
        m_canvas.RenderMode = SceneRenderMode.PointCloud
        m_canvas.ColorScheme = "viridis"
        m_canvas.UseEmbeddedColor = True
        m_canvas.ShowConnections = False
        m_canvas.ShowGround = False
        m_canvas.PointSize = 2
        m_canvas.PointAlpha = 255
        m_canvas.MultisampleCount = 1
        m_canvas.CullBackFaces = False
        m_canvas.EnableKeyboardShortcuts = True
        m_canvas.Name = "m_canvas"
        ' 
        ' m_split
        ' 
        m_split = New SplitContainer()
        m_split.Dock = DockStyle.Fill
        m_split.Orientation = Orientation.Vertical
        m_split.FixedPanel = FixedPanel.Panel2
        m_split.Name = "m_split"
        ' SplitterDistance / Panel1MinSize / Panel2MinSize 依赖真实布局尺寸，
        ' 在 FormMain_Load 的 adjustSplitter 中设置（构造函数阶段直接赋值会因未完成布局而抛异常）
        m_split.Panel1.Controls.Add(m_canvas)
        ' 
        ' sidebarPanel (侧边栏容器)
        ' 

        sidebarPanel.Dock = DockStyle.Fill
        sidebarPanel.ColumnCount = 1
        sidebarPanel.RowCount = 7
        sidebarPanel.Padding = New Padding(6)
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 26))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 38))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 34))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 112))

        legendLabel = New Label
        legendLabel.Text = "图例 / 筛选 (勾选控制显示)"
        legendLabel.Dock = DockStyle.Fill
        legendLabel.TextAlign = ContentAlignment.MiddleLeft
        legendLabel.Font = New System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)

        Call sidebarPanel.Controls.Add(legendLabel, 0, 0)
        ' 
        ' m_gradient
        ' 
        m_gradient = New PictureBox()
        m_gradient.Dock = DockStyle.Fill
        m_gradient.Height = 20
        m_gradient.Visible = False
        m_gradient.SizeMode = PictureBoxSizeMode.StretchImage
        m_gradient.Name = "m_gradient"
        Call sidebarPanel.Controls.Add(m_gradient, 0, 1)
        ' 
        ' m_legend
        ' 
        m_legend = New CheckedListBox()
        m_legend.Dock = DockStyle.Fill
        m_legend.CheckOnClick = True
        m_legend.IntegralHeight = False
        m_legend.Name = "m_legend"
        Call sidebarPanel.Controls.Add(m_legend, 0, 2)

        infoLabel = New Label
        infoLabel.Text = "神经元详情 (点击画布中的点)"
        infoLabel.Dock = DockStyle.Fill
        infoLabel.TextAlign = ContentAlignment.MiddleLeft
        infoLabel.Font = New System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)

        Call sidebarPanel.Controls.Add(infoLabel, 0, 3)
        ' 
        ' m_details
        ' 
        m_details = New TextBox()
        m_details.Dock = DockStyle.Fill
        m_details.Multiline = True
        m_details.ReadOnly = True
        m_details.ScrollBars = ScrollBars.Vertical
        m_details.Font = New System.Drawing.Font("Consolas", 9)
        m_details.BackColor = Color.FromArgb(250, 250, 250)
        m_details.Name = "m_details"
        Call sidebarPanel.Controls.Add(m_details, 0, 4)

        replayLabel = New Label
        replayLabel.Text = "电刺激 / 回放"
        replayLabel.Dock = DockStyle.Fill
        replayLabel.TextAlign = ContentAlignment.MiddleLeft
        replayLabel.Font = New System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)

        Call sidebarPanel.Controls.Add(replayLabel, 0, 5)
        ' 
        ' m_replayPanel (回放控制面板)
        ' 
        m_replayPanel = New TableLayoutPanel()
        m_replayPanel.Dock = DockStyle.Fill
        m_replayPanel.ColumnCount = 1
        m_replayPanel.RowCount = 3
        m_replayPanel.Margin = New Padding(0)
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30))
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30))
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        ' 
        ' replayButtonsPanel (回放按钮行)
        ' 

        replayButtonsPanel.Dock = DockStyle.Fill
        replayButtonsPanel.FlowDirection = FlowDirection.LeftToRight
        replayButtonsPanel.WrapContents = False
        replayButtonsPanel.Margin = New Padding(0)
        ' 
        ' m_replayFirst
        ' 
        m_replayFirst = New Button()
        m_replayFirst.Text = "|◀"
        m_replayFirst.Width = 40
        m_replayFirst.Height = 26
        m_replayFirst.Margin = New Padding(0, 0, 4, 0)
        m_replayFirst.TabStop = False
        m_replayFirst.Name = "m_replayFirst"
        ' 
        ' m_replayPrev
        ' 
        m_replayPrev = New Button()
        m_replayPrev.Text = "◀"
        m_replayPrev.Width = 40
        m_replayPrev.Height = 26
        m_replayPrev.Margin = New Padding(0, 0, 4, 0)
        m_replayPrev.TabStop = False
        m_replayPrev.Name = "m_replayPrev"
        ' 
        ' m_replayPlay
        ' 
        m_replayPlay = New Button()
        m_replayPlay.Text = "播放"
        m_replayPlay.Width = 52
        m_replayPlay.Height = 26
        m_replayPlay.Margin = New Padding(0, 0, 4, 0)
        m_replayPlay.TabStop = False
        m_replayPlay.Name = "m_replayPlay"
        ' 
        ' m_replayNext
        ' 
        m_replayNext = New Button()
        m_replayNext.Text = "▶"
        m_replayNext.Width = 40
        m_replayNext.Height = 26
        m_replayNext.Margin = New Padding(0, 0, 4, 0)
        m_replayNext.TabStop = False
        m_replayNext.Name = "m_replayNext"
        ' 
        ' m_replayLast
        ' 
        m_replayLast = New Button()
        m_replayLast.Text = "▶|"
        m_replayLast.Width = 40
        m_replayLast.Height = 26
        m_replayLast.Margin = New Padding(0, 0, 4, 0)
        m_replayLast.TabStop = False
        m_replayLast.Name = "m_replayLast"

        Call replayButtonsPanel.Controls.Add(m_replayFirst)
        Call replayButtonsPanel.Controls.Add(m_replayPrev)
        Call replayButtonsPanel.Controls.Add(m_replayPlay)
        Call replayButtonsPanel.Controls.Add(m_replayNext)
        Call replayButtonsPanel.Controls.Add(m_replayLast)
        ' 
        ' m_replayTrack
        ' 
        m_replayTrack = New TrackBar()
        m_replayTrack.Dock = DockStyle.Fill
        m_replayTrack.Minimum = 0
        m_replayTrack.Maximum = 0
        m_replayTrack.TickStyle = TickStyle.None
        m_replayTrack.SmallChange = 1
        m_replayTrack.LargeChange = 5
        m_replayTrack.Name = "m_replayTrack"
        ' 
        ' replayBottomPanel (速度 + 清除 + 状态文本)
        ' 

        replayBottomPanel.Dock = DockStyle.Fill
        replayBottomPanel.ColumnCount = 3
        replayBottomPanel.RowCount = 1
        replayBottomPanel.Margin = New Padding(0)
        replayBottomPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 34))
        replayBottomPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 86))
        replayBottomPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        ' 
        ' m_replaySpeed
        ' 
        m_replaySpeed = New NumericUpDown()
        m_replaySpeed.Minimum = 30
        m_replaySpeed.Maximum = 1000
        m_replaySpeed.Increment = 20
        m_replaySpeed.Value = 120
        m_replaySpeed.Width = 62
        m_replaySpeed.Margin = New Padding(0)
        m_replaySpeed.Name = "m_replaySpeed"
        ' 
        ' m_replayClear
        ' 
        m_replayClear = New Button()
        m_replayClear.Text = "清除"
        m_replayClear.Dock = DockStyle.Fill
        m_replayClear.Margin = New Padding(4, 0, 0, 0)
        m_replayClear.Name = "m_replayClear"
        ' 
        ' m_replayText
        ' 
        m_replayText = New Label()
        m_replayText.Dock = DockStyle.Fill
        m_replayText.TextAlign = ContentAlignment.MiddleLeft
        m_replayText.AutoEllipsis = True
        m_replayText.Margin = New Padding(6, 0, 0, 0)
        m_replayText.Name = "m_replayText"

        vlabel.Text = "速度"
        vlabel.Dock = DockStyle.Fill
        vlabel.TextAlign = ContentAlignment.MiddleLeft

        Call replayBottomPanel.Controls.Add(vlabel, 0, 0)
        Call replayBottomPanel.Controls.Add(m_replaySpeed, 1, 0)
        Call replayBottomPanel.Controls.Add(m_replayText, 2, 0)

        Call m_replayPanel.Controls.Add(replayButtonsPanel, 0, 0)
        Call m_replayPanel.Controls.Add(m_replayTrack, 0, 1)
        Call m_replayPanel.Controls.Add(replayBottomPanel, 0, 2)
        ' 清除按钮放在按钮行最右侧
        Call replayButtonsPanel.Controls.Add(m_replayClear)
        Call sidebarPanel.Controls.Add(m_replayPanel, 0, 6)
        Call m_split.Panel2.Controls.Add(sidebarPanel)
        ' 
        ' m_toolStrip
        ' 工具条：停靠在窗体顶端，位于菜单栏 (y=0, 高 24) 之下，故 y=24；高度 25 为 16x16 图标的标准工具条高度
        ' 
        m_toolStrip = New ToolStrip()
        m_toolStrip.Dock = DockStyle.Top
        m_toolStrip.GripStyle = ToolStripGripStyle.Hidden
        m_toolStrip.ImageScalingSize = New Size(16, 16)
        m_toolStrip.Location = New Point(0, 24)
        m_toolStrip.Name = "m_toolStrip"
        m_toolStrip.Size = New Size(1500, 25)
        m_toolStrip.TabIndex = 2
        ' 
        ' m_dimensionBox
        ' 
        m_dimensionBox = New ToolStripComboBox()
        m_dimensionBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_dimensionBox.Name = "m_dimensionBox"
        m_dimensionBox.Size = New Size(150, 23)
        m_dimensionBox.Items.AddRange(New Object() {"主导脑区 (neuropil)", "神经递质", "细胞类型", "分类层级", "仿真活跃度"})
        m_dimensionBox.SelectedIndex = 0
        ' 
        ' m_connectionBox
        ' 
        m_connectionBox = New ToolStripComboBox()
        m_connectionBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_connectionBox.Name = "m_connectionBox"
        m_connectionBox.Size = New Size(150, 23)
        m_connectionBox.Items.AddRange(New Object() {"连接 (逐条)", "脑区宏连接", "选中神经元的连接"})
        m_connectionBox.SelectedIndex = 0
        ' 
        ' m_renderModeBox
        ' 
        m_renderModeBox = New ToolStripComboBox()
        m_renderModeBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_renderModeBox.Name = "m_renderModeBox"
        m_renderModeBox.Size = New Size(110, 23)
        m_renderModeBox.Items.AddRange(New Object() {"点云", "线框", "实体"})
        m_renderModeBox.SelectedIndex = 0
        ' 
        ' m_lineColorBox
        ' 
        m_lineColorBox = New ToolStripComboBox()
        m_lineColorBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_lineColorBox.Name = "m_lineColorBox"
        m_lineColorBox.Size = New Size(130, 23)
        m_lineColorBox.Items.AddRange(New Object() {"连线: 前突触颜色", "连线: 递质类型", "连线: 单色"})
        m_lineColorBox.SelectedIndex = 0
        ' 
        ' m_thresholdBox
        ' 
        'm_thresholdBox = New NumericUpDown()
        'm_thresholdBox.Minimum = 1
        'm_thresholdBox.Maximum = 100000
        'm_thresholdBox.Increment = 10
        'm_thresholdBox.Value = m_buildOptions.SynapseThreshold
        'm_thresholdBox.Name = "m_thresholdBox"
        'm_thresholdBox.Size = New Size(80, 23)
        ' 
        ' m_pointSizeBox
        ' 
        'm_pointSizeBox = New NumericUpDown()
        'm_pointSizeBox.Minimum = 1
        'm_pointSizeBox.Maximum = 12
        'm_pointSizeBox.Increment = 1
        'm_pointSizeBox.Value = 2
        'm_pointSizeBox.Name = "m_pointSizeBox"
        'm_pointSizeBox.Size = New Size(56, 23)
        ' 
        ' m_showConnections
        ' 
        'm_showConnections = New ToolStripButton()
        'm_showConnections.CheckOnClick = True
        'm_showConnections.Checked = False
        'm_showConnections.Name = "m_showConnections"
        'm_showConnections.Size = New Size(75, 22)
        'm_showConnections.Text = "显示连接"
        ' 
        ' m_showGround
        ' 
        'm_showGround = New ToolStripButton()
        'm_showGround.CheckOnClick = True
        'm_showGround.Checked = False
        'm_showGround.Name = "m_showGround"
        'm_showGround.Size = New Size(45, 22)
        'm_showGround.Text = "地面"
        ' 
        ' m_snapshotButton
        ' 
        'm_snapshotButton = New ToolStripButton()
        'm_snapshotButton.Name = "m_snapshotButton"
        'm_snapshotButton.Size = New Size(45, 22)
        'm_snapshotButton.Text = "截图"
        '' 
        '' m_reloadButton
        '' 
        'm_reloadButton = New ToolStripButton()
        'm_reloadButton.Name = "m_reloadButton"
        'm_reloadButton.Size = New Size(60, 22)
        'm_reloadButton.Text = "重新载入"
        ' 
        ' m_stimulateMode
        ' 
        'm_stimulateMode = New ToolStripButton()
        'm_stimulateMode.CheckOnClick = True
        'm_stimulateMode.Checked = False
        'm_stimulateMode.Name = "m_stimulateMode"
        'm_stimulateMode.Size = New Size(75, 22)
        'm_stimulateMode.Text = "电刺激模式"
        'm_stimulateMode.ToolTipText = "勾选后：在神经元上按住左键（越久越强），松开即运行一次全脑 SNN 仿真并回放激活过程"
        ' 
        ' m_stimStrengthBox
        ' 
        'm_stimStrengthBox = New NumericUpDown()
        'm_stimStrengthBox.DecimalPlaces = 1
        'm_stimStrengthBox.Minimum = CDec(0.2)
        'm_stimStrengthBox.Maximum = CDec(20)
        'm_stimStrengthBox.Increment = CDec(0.5)
        'm_stimStrengthBox.Value = CDec(1)
        'm_stimStrengthBox.Name = "m_stimStrengthBox"
        'm_stimStrengthBox.Size = New Size(56, 23)
        ' 
        ' m_stimStepsBox
        ' 
        'm_stimStepsBox = New NumericUpDown()
        'm_stimStepsBox.Minimum = 5
        'm_stimStepsBox.Maximum = 200
        'm_stimStepsBox.Increment = 5
        'm_stimStepsBox.Value = 30
        'm_stimStepsBox.Name = "m_stimStepsBox"
        'm_stimStepsBox.Size = New Size(56, 23)
        ' 
        ' m_holdLabel
        ' 
        m_holdLabel = New ToolStripLabel()
        m_holdLabel.Name = "m_holdLabel"
        m_holdLabel.Size = New Size(39, 22)
        m_holdLabel.Text = ""
        ' 
        ' m_toolStrip 容器装配
        ' 

        colorLabel = New ToolStripLabel
        colorLabel.Text = "神经元着色："

        modeLabel = New ToolStripLabel
        modeLabel.Text = "模式："

        connLabel = New ToolStripLabel
        connLabel.Text = "连接："

        sep111 = New ToolStripSeparator

        lineColorLabel = New ToolStripLabel
        lineColorLabel.Text = "神经元链接颜色："

        m_toolStrip.SuspendLayout()
        m_toolStrip.Items.Add(colorLabel)
        m_toolStrip.Items.Add(m_dimensionBox)
        m_toolStrip.Items.Add(sep111)
        m_toolStrip.Items.Add(modeLabel)
        m_toolStrip.Items.Add(m_renderModeBox)
        m_toolStrip.Items.Add(connLabel)
        m_toolStrip.Items.Add(m_connectionBox)
        m_toolStrip.Items.Add(lineColorLabel)
        m_toolStrip.Items.Add(m_lineColorBox)
        m_toolStrip.Items.Add(New ToolStripLabel("≥突触:"))
        'm_toolStrip.Items.Add(New ToolStripControlHost(m_thresholdBox))
        m_toolStrip.Items.Add(New ToolStripSeparator())
        m_toolStrip.Items.Add(New ToolStripLabel("点大小:"))
        ' m_toolStrip.Items.Add(New ToolStripControlHost(m_pointSizeBox))
        '  m_toolStrip.Items.Add(m_showConnections)
        '  m_toolStrip.Items.Add(m_showGround)
        m_toolStrip.Items.Add(New ToolStripSeparator())
        '   m_toolStrip.Items.Add(m_snapshotButton)
        '   m_toolStrip.Items.Add(m_reloadButton)
        m_toolStrip.Items.Add(New ToolStripSeparator())
        ' m_toolStrip.Items.Add(m_stimulateMode)
        m_toolStrip.Items.Add(New ToolStripLabel("强度×"))
        '  m_toolStrip.Items.Add(New ToolStripControlHost(m_stimStrengthBox))
        m_toolStrip.Items.Add(New ToolStripLabel("仿真步数"))
        ' m_toolStrip.Items.Add(New ToolStripControlHost(m_stimStepsBox))
        m_toolStrip.Items.Add(m_holdLabel)
        m_toolStrip.ResumeLayout(False)

        ' 控件本身（菜单栏 / 状态栏 / 工具条 / 画布 / 分隔容器 / 侧边栏 / 回放面板）已在
        ' InitializeComponent 中声明式建好；这里只负责把根容器按顺序挂到窗体上。
        ' 顺序保持：split → 工具条 → 菜单 → 状态栏
        Me.Controls.Add(m_split)
        Me.Controls.Add(m_toolStrip)
        ' Me.Controls.Add(m_menuStrip)
        ' Me.Controls.Add(m_statusStrip)

        ResumeLayout(False)
    End Sub

    Dim colorLabel As ToolStripLabel
    Dim modeLabel As ToolStripLabel
    Dim connLabel As ToolStripLabel
    Dim lineColorLabel As ToolStripLabel
    Dim sep111 As ToolStripSeparator

End Class
