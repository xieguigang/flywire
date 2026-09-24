Imports Microsoft.VisualBasic.Drawing.DirectX
Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D

' FormMain 的界面布局（Windows 窗体设计器维护的声明式代码）。
' 这里只放"控件怎么摆"的代码：控件实例化、属性赋值、容器装配与事件声明；
' 数据载入、场景装配、拾取、图例与电刺激等逻辑仍然留在 FormMain.vb 里。
'
' 所有位置 / 大小 / 文本 / 颜色都写成常数字面量（与 ResponseChartForm.Designer.vb 一致），
' 事件绑定一律用 Handles 声明在 FormMain.vb 的处理方法上。
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class FormMain
    Inherits System.Windows.Forms.Form

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

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        m_split = New SplitContainer()
        m_canvas = New DxScene3DCanvas()
        sidebar = New TableLayoutPanel()
        legendLabel = New Label()
        m_gradient = New PictureBox()
        m_legend = New CheckedListBox()
        detailsLabel = New Label()
        m_details = New TextBox()
        replayLabel = New Label()
        m_replayPanel = New TableLayoutPanel()
        replayButtons = New FlowLayoutPanel()
        m_replayFirst = New Button()
        m_replayPrev = New Button()
        m_replayPlay = New Button()
        m_replayNext = New Button()
        m_replayLast = New Button()
        m_replayClear = New Button()
        m_replayTrack = New TrackBar()
        replayBottom = New TableLayoutPanel()
        speedLabel = New Label()
        m_replaySpeed = New NumericUpDown()
        m_replayText = New Label()
        mainMenu = New MenuStrip()
        fileMenu = New ToolStripMenuItem()
        openMenuItem = New ToolStripMenuItem()
        snapshotMenuItem = New ToolStripMenuItem()
        exitMenuItem = New ToolStripMenuItem()
        viewMenu = New ToolStripMenuItem()
        resetViewMenuItem = New ToolStripMenuItem()
        helpMenu = New ToolStripMenuItem()
        aboutMenuItem = New ToolStripMenuItem()
        toolbar = New ToolStrip()
        dimensionLabel = New ToolStripLabel()
        m_dimensionBox = New ToolStripComboBox()
        modeLabel = New ToolStripLabel()
        m_renderModeBox = New ToolStripComboBox()
        connectionLabel = New ToolStripLabel()
        m_connectionBox = New ToolStripComboBox()
        m_lineColorBox = New ToolStripComboBox()
        thresholdLabel = New ToolStripLabel()
        m_thresholdHost = New NumericUpDown()
        pointSizeLabel = New ToolStripLabel()
        m_pointSizeHost = New NumericUpDown()
        m_showConnections = New ToolStripButton()
        m_showGround = New ToolStripButton()
        snapshotButton = New ToolStripButton()
        reloadButton = New ToolStripButton()
        m_stimulateMode = New ToolStripButton()
        stimStrengthLabel = New ToolStripLabel()
        m_stimStrengthHost = New NumericUpDown()
        stimStepsLabel = New ToolStripLabel()
        m_stimStepsHost = New NumericUpDown()
        m_holdLabel = New ToolStripLabel()
        statusBar = New StatusStrip()
        m_statusText = New ToolStripStatusLabel()
        m_sceneText = New ToolStripStatusLabel()
        m_progress = New ToolStripProgressBar()
        m_chartLinkHost = New LinkLabel()
        m_snakeLinkHost = New LinkLabel()
        m_chartTip = New ToolTip(components)
        m_rebuildTimer = New Timer(components)
        CType(m_split, ComponentModel.ISupportInitialize).BeginInit()
        m_split.Panel1.SuspendLayout()
        m_split.Panel2.SuspendLayout()
        m_split.SuspendLayout()
        sidebar.SuspendLayout()
        CType(m_gradient, ComponentModel.ISupportInitialize).BeginInit()
        m_replayPanel.SuspendLayout()
        replayButtons.SuspendLayout()
        CType(m_replayTrack, ComponentModel.ISupportInitialize).BeginInit()
        replayBottom.SuspendLayout()
        CType(m_replaySpeed, ComponentModel.ISupportInitialize).BeginInit()
        mainMenu.SuspendLayout()
        toolbar.SuspendLayout()
        CType(m_thresholdHost, ComponentModel.ISupportInitialize).BeginInit()
        CType(m_pointSizeHost, ComponentModel.ISupportInitialize).BeginInit()
        CType(m_stimStrengthHost, ComponentModel.ISupportInitialize).BeginInit()
        CType(m_stimStepsHost, ComponentModel.ISupportInitialize).BeginInit()
        statusBar.SuspendLayout()
        SuspendLayout()
        ' 
        ' m_split
        ' 
        m_split.Dock = DockStyle.Fill
        m_split.FixedPanel = FixedPanel.Panel2
        m_split.Location = New Point(0, 50)
        m_split.Name = "m_split"
        ' 
        ' m_split.Panel1
        ' 
        m_split.Panel1.Controls.Add(m_canvas)
        ' 
        ' m_split.Panel2
        ' 
        m_split.Panel2.Controls.Add(sidebar)
        m_split.Size = New Size(1500, 828)
        m_split.SplitterDistance = 1400
        m_split.TabIndex = 0
        ' 
        ' m_canvas
        ' 
        m_canvas.AutoClear = False
        m_canvas.BackColor = Color.Black
        m_canvas.BackgroundColor = Color.FromArgb(CByte(12), CByte(12), CByte(18))
        m_canvas.Dock = DockStyle.Fill
        m_canvas.Location = New Point(0, 0)
        m_canvas.Name = "m_canvas"
        m_canvas.RenderMode = SceneRenderMode.PointCloud
        m_canvas.ShowConnections = False
        m_canvas.ShowGround = False
        m_canvas.Size = New Size(1400, 828)
        m_canvas.TabIndex = 0
        m_canvas.UseEmbeddedColor = True
        ' 
        ' sidebar
        ' 
        sidebar.ColumnCount = 1
        sidebar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100F))
        sidebar.Controls.Add(legendLabel, 0, 0)
        sidebar.Controls.Add(m_gradient, 0, 1)
        sidebar.Controls.Add(m_legend, 0, 2)
        sidebar.Controls.Add(detailsLabel, 0, 3)
        sidebar.Controls.Add(m_details, 0, 4)
        sidebar.Controls.Add(replayLabel, 0, 5)
        sidebar.Controls.Add(m_replayPanel, 0, 6)
        sidebar.Dock = DockStyle.Fill
        sidebar.Location = New Point(0, 0)
        sidebar.Name = "sidebar"
        sidebar.Padding = New Padding(6)
        sidebar.RowCount = 7
        sidebar.RowStyles.Add(New RowStyle(SizeType.Absolute, 24F))
        sidebar.RowStyles.Add(New RowStyle(SizeType.Absolute, 26F))
        sidebar.RowStyles.Add(New RowStyle(SizeType.Percent, 38F))
        sidebar.RowStyles.Add(New RowStyle(SizeType.Absolute, 24F))
        sidebar.RowStyles.Add(New RowStyle(SizeType.Percent, 34F))
        sidebar.RowStyles.Add(New RowStyle(SizeType.Absolute, 24F))
        sidebar.RowStyles.Add(New RowStyle(SizeType.Absolute, 112F))
        sidebar.Size = New Size(96, 828)
        sidebar.TabIndex = 0
        ' 
        ' legendLabel
        ' 
        legendLabel.Dock = DockStyle.Fill
        legendLabel.Font = New Font("Segoe UI", 9F, FontStyle.Bold)
        legendLabel.Location = New Point(9, 6)
        legendLabel.Name = "legendLabel"
        legendLabel.Size = New Size(78, 24)
        legendLabel.TabIndex = 0
        legendLabel.Text = "图例 / 筛选 (勾选控制显示)"
        legendLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_gradient
        ' 
        m_gradient.Dock = DockStyle.Fill
        m_gradient.Location = New Point(9, 33)
        m_gradient.Name = "m_gradient"
        m_gradient.Size = New Size(78, 20)
        m_gradient.SizeMode = PictureBoxSizeMode.StretchImage
        m_gradient.TabIndex = 1
        m_gradient.TabStop = False
        m_gradient.Visible = False
        ' 
        ' m_legend
        ' 
        m_legend.CheckOnClick = True
        m_legend.Dock = DockStyle.Fill
        m_legend.IntegralHeight = False
        m_legend.Location = New Point(9, 59)
        m_legend.Name = "m_legend"
        m_legend.Size = New Size(78, 313)
        m_legend.TabIndex = 2
        ' 
        ' detailsLabel
        ' 
        detailsLabel.Dock = DockStyle.Fill
        detailsLabel.Font = New Font("Segoe UI", 9F, FontStyle.Bold)
        detailsLabel.Location = New Point(9, 375)
        detailsLabel.Name = "detailsLabel"
        detailsLabel.Size = New Size(78, 24)
        detailsLabel.TabIndex = 3
        detailsLabel.Text = "神经元详情 (点击画布中的点)"
        detailsLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_details
        ' 
        m_details.BackColor = Color.FromArgb(CByte(250), CByte(250), CByte(250))
        m_details.Dock = DockStyle.Fill
        m_details.Font = New Font("Consolas", 9F)
        m_details.Location = New Point(9, 402)
        m_details.Multiline = True
        m_details.Name = "m_details"
        m_details.ReadOnly = True
        m_details.ScrollBars = ScrollBars.Vertical
        m_details.Size = New Size(78, 280)
        m_details.TabIndex = 4
        ' 
        ' replayLabel
        ' 
        replayLabel.Dock = DockStyle.Fill
        replayLabel.Font = New Font("Segoe UI", 9F, FontStyle.Bold)
        replayLabel.Location = New Point(9, 685)
        replayLabel.Name = "replayLabel"
        replayLabel.Size = New Size(78, 24)
        replayLabel.TabIndex = 5
        replayLabel.Text = "电刺激 / 回放"
        replayLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_replayPanel
        ' 
        m_replayPanel.ColumnCount = 1
        m_replayPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100F))
        m_replayPanel.Controls.Add(replayButtons, 0, 0)
        m_replayPanel.Controls.Add(m_replayTrack, 0, 1)
        m_replayPanel.Controls.Add(replayBottom, 0, 2)
        m_replayPanel.Dock = DockStyle.Fill
        m_replayPanel.Location = New Point(6, 709)
        m_replayPanel.Margin = New Padding(0)
        m_replayPanel.Name = "m_replayPanel"
        m_replayPanel.RowCount = 3
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30F))
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30F))
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100F))
        m_replayPanel.Size = New Size(84, 113)
        m_replayPanel.TabIndex = 6
        ' 
        ' replayButtons
        ' 
        replayButtons.Controls.Add(m_replayFirst)
        replayButtons.Controls.Add(m_replayPrev)
        replayButtons.Controls.Add(m_replayPlay)
        replayButtons.Controls.Add(m_replayNext)
        replayButtons.Controls.Add(m_replayLast)
        replayButtons.Controls.Add(m_replayClear)
        replayButtons.Dock = DockStyle.Fill
        replayButtons.Location = New Point(0, 0)
        replayButtons.Margin = New Padding(0)
        replayButtons.Name = "replayButtons"
        replayButtons.Size = New Size(84, 30)
        replayButtons.TabIndex = 0
        replayButtons.WrapContents = False
        ' 
        ' m_replayFirst
        ' 
        m_replayFirst.Location = New Point(0, 0)
        m_replayFirst.Margin = New Padding(0, 0, 4, 0)
        m_replayFirst.Name = "m_replayFirst"
        m_replayFirst.Size = New Size(40, 26)
        m_replayFirst.TabIndex = 0
        m_replayFirst.TabStop = False
        m_replayFirst.Text = "|◀"
        ' 
        ' m_replayPrev
        ' 
        m_replayPrev.Location = New Point(44, 0)
        m_replayPrev.Margin = New Padding(0, 0, 4, 0)
        m_replayPrev.Name = "m_replayPrev"
        m_replayPrev.Size = New Size(40, 26)
        m_replayPrev.TabIndex = 1
        m_replayPrev.TabStop = False
        m_replayPrev.Text = "◀"
        ' 
        ' m_replayPlay
        ' 
        m_replayPlay.Location = New Point(88, 0)
        m_replayPlay.Margin = New Padding(0, 0, 4, 0)
        m_replayPlay.Name = "m_replayPlay"
        m_replayPlay.Size = New Size(52, 26)
        m_replayPlay.TabIndex = 2
        m_replayPlay.TabStop = False
        m_replayPlay.Text = "播放"
        ' 
        ' m_replayNext
        ' 
        m_replayNext.Location = New Point(144, 0)
        m_replayNext.Margin = New Padding(0, 0, 4, 0)
        m_replayNext.Name = "m_replayNext"
        m_replayNext.Size = New Size(40, 26)
        m_replayNext.TabIndex = 3
        m_replayNext.TabStop = False
        m_replayNext.Text = "▶"
        ' 
        ' m_replayLast
        ' 
        m_replayLast.Location = New Point(188, 0)
        m_replayLast.Margin = New Padding(0, 0, 4, 0)
        m_replayLast.Name = "m_replayLast"
        m_replayLast.Size = New Size(40, 26)
        m_replayLast.TabIndex = 4
        m_replayLast.TabStop = False
        m_replayLast.Text = "▶|"
        ' 
        ' m_replayClear
        ' 
        m_replayClear.Dock = DockStyle.Fill
        m_replayClear.Location = New Point(236, 0)
        m_replayClear.Margin = New Padding(4, 0, 0, 0)
        m_replayClear.Name = "m_replayClear"
        m_replayClear.Size = New Size(75, 26)
        m_replayClear.TabIndex = 5
        m_replayClear.TabStop = False
        m_replayClear.Text = "清除"
        ' 
        ' m_replayTrack
        ' 
        m_replayTrack.Dock = DockStyle.Fill
        m_replayTrack.Location = New Point(3, 33)
        m_replayTrack.Maximum = 0
        m_replayTrack.Name = "m_replayTrack"
        m_replayTrack.Size = New Size(78, 24)
        m_replayTrack.TabIndex = 1
        m_replayTrack.TickStyle = TickStyle.None
        ' 
        ' replayBottom
        ' 
        replayBottom.ColumnCount = 3
        replayBottom.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 34F))
        replayBottom.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 86F))
        replayBottom.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100F))
        replayBottom.Controls.Add(speedLabel, 0, 0)
        replayBottom.Controls.Add(m_replaySpeed, 1, 0)
        replayBottom.Controls.Add(m_replayText, 2, 0)
        replayBottom.Dock = DockStyle.Fill
        replayBottom.Location = New Point(0, 60)
        replayBottom.Margin = New Padding(0)
        replayBottom.Name = "replayBottom"
        replayBottom.RowCount = 1
        replayBottom.RowStyles.Add(New RowStyle(SizeType.Percent, 100F))
        replayBottom.Size = New Size(84, 53)
        replayBottom.TabIndex = 2
        ' 
        ' speedLabel
        ' 
        speedLabel.Dock = DockStyle.Fill
        speedLabel.Location = New Point(0, 0)
        speedLabel.Margin = New Padding(0)
        speedLabel.Name = "speedLabel"
        speedLabel.Size = New Size(34, 53)
        speedLabel.TabIndex = 0
        speedLabel.Text = "速度"
        speedLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_replaySpeed
        ' 
        m_replaySpeed.Location = New Point(34, 0)
        m_replaySpeed.Margin = New Padding(0)
        m_replaySpeed.Maximum = New Decimal(New Integer() {1000, 0, 0, 0})
        m_replaySpeed.Minimum = New Decimal(New Integer() {30, 0, 0, 0})
        m_replaySpeed.Name = "m_replaySpeed"
        m_replaySpeed.Size = New Size(62, 23)
        m_replaySpeed.TabIndex = 1
        m_replaySpeed.Value = New Decimal(New Integer() {120, 0, 0, 0})
        ' 
        ' m_replayText
        ' 
        m_replayText.AutoEllipsis = True
        m_replayText.Dock = DockStyle.Fill
        m_replayText.Location = New Point(126, 0)
        m_replayText.Margin = New Padding(6, 0, 0, 0)
        m_replayText.Name = "m_replayText"
        m_replayText.Size = New Size(1, 53)
        m_replayText.TabIndex = 2
        m_replayText.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' mainMenu
        ' 
        mainMenu.Items.AddRange(New ToolStripItem() {fileMenu, viewMenu, helpMenu})
        mainMenu.Location = New Point(0, 0)
        mainMenu.Name = "mainMenu"
        mainMenu.Size = New Size(1500, 24)
        mainMenu.TabIndex = 0
        ' 
        ' fileMenu
        ' 
        fileMenu.DropDownItems.AddRange(New ToolStripItem() {openMenuItem, snapshotMenuItem, exitMenuItem})
        fileMenu.Name = "fileMenu"
        fileMenu.Size = New Size(62, 20)
        fileMenu.Text = "文件 (&F)"
        ' 
        ' openMenuItem
        ' 
        openMenuItem.Name = "openMenuItem"
        openMenuItem.Size = New Size(181, 22)
        openMenuItem.Text = "打开数据目录 (&O)..."
        ' 
        ' snapshotMenuItem
        ' 
        snapshotMenuItem.Name = "snapshotMenuItem"
        snapshotMenuItem.Size = New Size(181, 22)
        snapshotMenuItem.Text = "保存截图 (&S)..."
        ' 
        ' exitMenuItem
        ' 
        exitMenuItem.Name = "exitMenuItem"
        exitMenuItem.Size = New Size(181, 22)
        exitMenuItem.Text = "退出 (&X)"
        ' 
        ' viewMenu
        ' 
        viewMenu.DropDownItems.AddRange(New ToolStripItem() {resetViewMenuItem})
        viewMenu.Name = "viewMenu"
        viewMenu.Size = New Size(63, 20)
        viewMenu.Text = "视图 (&V)"
        ' 
        ' resetViewMenuItem
        ' 
        resetViewMenuItem.Name = "resetViewMenuItem"
        resetViewMenuItem.Size = New Size(144, 22)
        resetViewMenuItem.Text = "重置视角 (&R)"
        ' 
        ' helpMenu
        ' 
        helpMenu.DropDownItems.AddRange(New ToolStripItem() {aboutMenuItem})
        helpMenu.Name = "helpMenu"
        helpMenu.Size = New Size(65, 20)
        helpMenu.Text = "帮助 (&H)"
        ' 
        ' aboutMenuItem
        ' 
        aboutMenuItem.Name = "aboutMenuItem"
        aboutMenuItem.Size = New Size(171, 22)
        aboutMenuItem.Text = "关于数据来源 (&A)"
        ' 
        ' toolbar
        ' 
        toolbar.GripStyle = ToolStripGripStyle.Hidden
        toolbar.Items.AddRange(New ToolStripItem() {dimensionLabel, m_dimensionBox, modeLabel, m_renderModeBox, connectionLabel, m_connectionBox, m_lineColorBox, thresholdLabel, m_thresholdHost, pointSizeLabel, m_pointSizeHost, m_showConnections, m_showGround, snapshotButton, reloadButton, m_stimulateMode, stimStrengthLabel, m_stimStrengthHost, stimStepsLabel, m_stimStepsHost, m_holdLabel})
        toolbar.Location = New Point(0, 24)
        toolbar.Name = "toolbar"
        toolbar.Size = New Size(1500, 26)
        toolbar.TabIndex = 1
        ' 
        ' dimensionLabel
        ' 
        dimensionLabel.Name = "dimensionLabel"
        dimensionLabel.Size = New Size(36, 23)
        dimensionLabel.Text = "着色:"
        ' 
        ' m_dimensionBox
        ' 
        m_dimensionBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_dimensionBox.Items.AddRange(New Object() {"主导脑区 (neuropil)", "神经递质", "细胞类型", "分类层级", "仿真活跃度"})
        m_dimensionBox.Name = "m_dimensionBox"
        m_dimensionBox.Size = New Size(121, 26)
        m_dimensionBox.Text = "主导脑区 (neuropil)"
        ' 
        ' modeLabel
        ' 
        modeLabel.Name = "modeLabel"
        modeLabel.Size = New Size(36, 23)
        modeLabel.Text = "模式:"
        ' 
        ' m_renderModeBox
        ' 
        m_renderModeBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_renderModeBox.Items.AddRange(New Object() {"点云", "线框", "实体"})
        m_renderModeBox.Name = "m_renderModeBox"
        m_renderModeBox.Size = New Size(121, 26)
        m_renderModeBox.Text = "点云"
        ' 
        ' connectionLabel
        ' 
        connectionLabel.Name = "connectionLabel"
        connectionLabel.Size = New Size(36, 23)
        connectionLabel.Text = "连接:"
        ' 
        ' m_connectionBox
        ' 
        m_connectionBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_connectionBox.Items.AddRange(New Object() {"连接 (逐条)", "脑区宏连接", "选中神经元的连接"})
        m_connectionBox.Name = "m_connectionBox"
        m_connectionBox.Size = New Size(121, 26)
        m_connectionBox.Text = "连接 (逐条)"
        ' 
        ' m_lineColorBox
        ' 
        m_lineColorBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_lineColorBox.Items.AddRange(New Object() {"连线: 前突触颜色", "连线: 递质类型", "连线: 单色"})
        m_lineColorBox.Name = "m_lineColorBox"
        m_lineColorBox.Size = New Size(121, 26)
        m_lineColorBox.Text = "连线: 前突触颜色"
        ' 
        ' thresholdLabel
        ' 
        thresholdLabel.Name = "thresholdLabel"
        thresholdLabel.Size = New Size(44, 23)
        thresholdLabel.Text = "≥突触:"
        ' 
        ' m_thresholdHost
        ' 
        m_thresholdHost.AccessibleName = "m_thresholdHost"
        m_thresholdHost.Increment = New Decimal(New Integer() {10, 0, 0, 0})
        m_thresholdHost.Location = New Point(650, 1)
        m_thresholdHost.Maximum = New Decimal(New Integer() {100000, 0, 0, 0})
        m_thresholdHost.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        m_thresholdHost.Name = "m_thresholdHost"
        m_thresholdHost.Size = New Size(59, 23)
        m_thresholdHost.TabIndex = 0
        m_thresholdHost.Value = New Decimal(New Integer() {20, 0, 0, 0})
        ' 
        ' m_thresholdHost
        ' 
        m_thresholdHost.Name = "m_thresholdHost"
        m_thresholdHost.Size = New Size(59, 23)
        m_thresholdHost.Text = "20"
        ' 
        ' pointSizeLabel
        ' 
        pointSizeLabel.Name = "pointSizeLabel"
        pointSizeLabel.Size = New Size(49, 23)
        pointSizeLabel.Text = "点大小:"
        ' 
        ' m_pointSizeHost
        ' 
        m_pointSizeHost.AccessibleName = "m_pointSizeHost"
        m_pointSizeHost.Location = New Point(764, 1)
        m_pointSizeHost.Maximum = New Decimal(New Integer() {12, 0, 0, 0})
        m_pointSizeHost.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        m_pointSizeHost.Name = "m_pointSizeHost"
        m_pointSizeHost.Size = New Size(35, 23)
        m_pointSizeHost.TabIndex = 0
        m_pointSizeHost.Value = New Decimal(New Integer() {2, 0, 0, 0})
        ' 
        ' m_pointSizeHost
        ' 
        m_pointSizeHost.Name = "m_pointSizeHost"
        m_pointSizeHost.Size = New Size(35, 23)
        m_pointSizeHost.Text = "2"
        ' 
        ' m_showConnections
        ' 
        m_showConnections.CheckOnClick = True
        m_showConnections.Name = "m_showConnections"
        m_showConnections.Size = New Size(63, 23)
        m_showConnections.Text = "显示连接"
        ' 
        ' m_showGround
        ' 
        m_showGround.CheckOnClick = True
        m_showGround.Name = "m_showGround"
        m_showGround.Size = New Size(37, 23)
        m_showGround.Text = "地面"
        ' 
        ' snapshotButton
        ' 
        snapshotButton.Name = "snapshotButton"
        snapshotButton.Size = New Size(37, 23)
        snapshotButton.Text = "截图"
        ' 
        ' reloadButton
        ' 
        reloadButton.Name = "reloadButton"
        reloadButton.Size = New Size(63, 23)
        reloadButton.Text = "重新载入"
        ' 
        ' m_stimulateMode
        ' 
        m_stimulateMode.CheckOnClick = True
        m_stimulateMode.Name = "m_stimulateMode"
        m_stimulateMode.Size = New Size(76, 23)
        m_stimulateMode.Text = "电刺激模式"
        m_stimulateMode.ToolTipText = "勾选后：在神经元上按住左键（越久越强），松开即运行一次全脑 SNN 仿真并回放激活过程"
        ' 
        ' stimStrengthLabel
        ' 
        stimStrengthLabel.Name = "stimStrengthLabel"
        stimStrengthLabel.Size = New Size(41, 23)
        stimStrengthLabel.Text = "强度×"
        ' 
        ' m_stimStrengthHost
        ' 
        m_stimStrengthHost.AccessibleName = "m_stimStrengthHost"
        m_stimStrengthHost.DecimalPlaces = 1
        m_stimStrengthHost.Increment = New Decimal(New Integer() {5, 0, 0, 65536})
        m_stimStrengthHost.Location = New Point(1128, 1)
        m_stimStrengthHost.Maximum = New Decimal(New Integer() {20, 0, 0, 0})
        m_stimStrengthHost.Minimum = New Decimal(New Integer() {2, 0, 0, 65536})
        m_stimStrengthHost.Name = "m_stimStrengthHost"
        m_stimStrengthHost.Size = New Size(44, 23)
        m_stimStrengthHost.TabIndex = 0
        m_stimStrengthHost.Value = New Decimal(New Integer() {1, 0, 0, 0})
        ' 
        ' m_stimStrengthHost
        ' 
        m_stimStrengthHost.Name = "m_stimStrengthHost"
        m_stimStrengthHost.Size = New Size(44, 23)
        m_stimStrengthHost.Text = "1.0"
        ' 
        ' stimStepsLabel
        ' 
        stimStepsLabel.Name = "stimStepsLabel"
        stimStepsLabel.Size = New Size(59, 23)
        stimStepsLabel.Text = "仿真步数"
        ' 
        ' m_stimStepsHost
        ' 
        m_stimStepsHost.AccessibleName = "m_stimStepsHost"
        m_stimStepsHost.Increment = New Decimal(New Integer() {5, 0, 0, 0})
        m_stimStepsHost.Location = New Point(1231, 1)
        m_stimStepsHost.Maximum = New Decimal(New Integer() {200, 0, 0, 0})
        m_stimStepsHost.Minimum = New Decimal(New Integer() {5, 0, 0, 0})
        m_stimStepsHost.Name = "m_stimStepsHost"
        m_stimStepsHost.Size = New Size(41, 23)
        m_stimStepsHost.TabIndex = 0
        m_stimStepsHost.Value = New Decimal(New Integer() {30, 0, 0, 0})
        ' 
        ' m_stimStepsHost
        ' 
        m_stimStepsHost.Name = "m_stimStepsHost"
        m_stimStepsHost.Size = New Size(41, 23)
        m_stimStepsHost.Text = "30"
        ' 
        ' m_holdLabel
        ' 
        m_holdLabel.Name = "m_holdLabel"
        m_holdLabel.Size = New Size(0, 23)
        ' 
        ' statusBar
        ' 
        statusBar.Items.AddRange(New ToolStripItem() {m_statusText, m_sceneText, m_progress, m_chartLinkHost, m_snakeLinkHost})
        statusBar.Location = New Point(0, 878)
        statusBar.Name = "statusBar"
        statusBar.Size = New Size(1500, 22)
        statusBar.TabIndex = 3
        ' 
        ' m_statusText
        ' 
        m_statusText.Name = "m_statusText"
        m_statusText.Size = New Size(1178, 17)
        m_statusText.Spring = True
        m_statusText.Text = "就绪"
        m_statusText.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_sceneText
        ' 
        m_sceneText.BorderSides = ToolStripStatusLabelBorderSides.Left
        m_sceneText.Name = "m_sceneText"
        m_sceneText.Size = New Size(4, 17)
        ' 
        ' m_progress
        ' 
        m_progress.Name = "m_progress"
        m_progress.Size = New Size(100, 16)
        m_progress.Style = ProgressBarStyle.Marquee
        m_progress.Visible = False
        ' 
        ' m_chartLinkHost
        ' 
        m_chartLinkHost.AccessibleName = "m_chartLinkHost"
        m_chartLinkHost.ActiveLinkColor = Color.White
        m_chartLinkHost.AutoSize = True
        m_chartLinkHost.DisabledLinkColor = Color.FromArgb(CByte(100), CByte(116), CByte(139))
        m_chartLinkHost.Enabled = False
        m_chartLinkHost.LinkBehavior = LinkBehavior.HoverUnderline
        m_chartLinkHost.LinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        m_chartLinkHost.Location = New Point(1285, 2)
        m_chartLinkHost.Margin = New Padding(6, 4, 6, 0)
        m_chartLinkHost.Name = "m_chartLinkHost"
        m_chartLinkHost.Size = New Size(59, 20)
        m_chartLinkHost.TabIndex = 0
        m_chartLinkHost.TabStop = True
        m_chartLinkHost.Text = "响应曲线"
        m_chartTip.SetToolTip(m_chartLinkHost, "把电刺激实验记录下来的响应结果画成曲线图（横轴时间步 / 纵轴响应电信号强度）")
        m_chartLinkHost.VisitedLinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        ' 
        ' m_chartLinkHost
        ' 
        m_chartLinkHost.Name = "m_chartLinkHost"
        m_chartLinkHost.Size = New Size(59, 20)
        m_chartLinkHost.Text = "响应曲线"
        ' 
        ' m_snakeLinkHost
        ' 
        m_snakeLinkHost.AccessibleName = "m_snakeLinkHost"
        m_snakeLinkHost.ActiveLinkColor = Color.White
        m_snakeLinkHost.AutoSize = True
        m_snakeLinkHost.LinkBehavior = LinkBehavior.HoverUnderline
        m_snakeLinkHost.LinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        m_snakeLinkHost.Location = New Point(1344, 2)
        m_snakeLinkHost.Margin = New Padding(6, 4, 6, 0)
        m_snakeLinkHost.Name = "m_snakeLinkHost"
        m_snakeLinkHost.Size = New Size(111, 20)
        m_snakeLinkHost.TabIndex = 1
        m_snakeLinkHost.TabStop = True
        m_snakeLinkHost.Text = "果蝇大脑玩贪吃蛇"
        m_chartTip.SetToolTip(m_snakeLinkHost, "让果蝇大脑模型接管贪吃蛇的运动：实时画面 + 大脑神经元活动（并同步点亮三维点云）")
        m_snakeLinkHost.VisitedLinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        ' 
        ' m_snakeLinkHost
        ' 
        m_snakeLinkHost.Name = "m_snakeLinkHost"
        m_snakeLinkHost.Size = New Size(111, 20)
        m_snakeLinkHost.Text = "果蝇大脑玩贪吃蛇"
        ' 
        ' m_rebuildTimer
        ' 
        m_rebuildTimer.Interval = 260
        ' 
        ' FormMain
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1500, 900)
        Controls.Add(m_split)
        Controls.Add(toolbar)
        Controls.Add(mainMenu)
        Controls.Add(statusBar)
        MinimumSize = New Size(900, 600)
        Name = "FormMain"
        StartPosition = FormStartPosition.CenterScreen
        Text = "Neuropils - Drosophila brain 3D viewer"
        m_split.Panel1.ResumeLayout(False)
        m_split.Panel2.ResumeLayout(False)
        CType(m_split, ComponentModel.ISupportInitialize).EndInit()
        m_split.ResumeLayout(False)
        sidebar.ResumeLayout(False)
        sidebar.PerformLayout()
        CType(m_gradient, ComponentModel.ISupportInitialize).EndInit()
        m_replayPanel.ResumeLayout(False)
        m_replayPanel.PerformLayout()
        replayButtons.ResumeLayout(False)
        CType(m_replayTrack, ComponentModel.ISupportInitialize).EndInit()
        replayBottom.ResumeLayout(False)
        CType(m_replaySpeed, ComponentModel.ISupportInitialize).EndInit()
        mainMenu.ResumeLayout(False)
        mainMenu.PerformLayout()
        toolbar.ResumeLayout(False)
        toolbar.PerformLayout()
        CType(m_thresholdHost, ComponentModel.ISupportInitialize).EndInit()
        CType(m_pointSizeHost, ComponentModel.ISupportInitialize).EndInit()
        CType(m_stimStrengthHost, ComponentModel.ISupportInitialize).EndInit()
        CType(m_stimStepsHost, ComponentModel.ISupportInitialize).EndInit()
        statusBar.ResumeLayout(False)
        statusBar.PerformLayout()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    ' ---- 只负责摆位的控件（声明即建好，属性在 InitializeComponent 里赋值）----
    Friend WithEvents m_split As SplitContainer
    Dim WithEvents sidebar As TableLayoutPanel
    Dim WithEvents legendLabel As Label
    Dim WithEvents detailsLabel As Label
    Dim WithEvents replayLabel As Label
    Dim WithEvents replayButtons As FlowLayoutPanel
    Friend WithEvents m_replayFirst As Button
    Friend WithEvents m_replayPrev As Button
    Friend WithEvents m_replayPlay As Button
    Friend WithEvents m_replayNext As Button
    Friend WithEvents m_replayLast As Button
    Friend WithEvents m_replayClear As Button
    Dim WithEvents replayBottom As TableLayoutPanel
    Dim WithEvents speedLabel As Label
    Friend WithEvents m_replayPanel As TableLayoutPanel
    Friend WithEvents m_replayText As Label
    Dim WithEvents mainMenu As MenuStrip
    Dim WithEvents fileMenu As ToolStripMenuItem
    Dim WithEvents openMenuItem As ToolStripMenuItem
    Dim WithEvents snapshotMenuItem As ToolStripMenuItem
    Dim WithEvents exitMenuItem As ToolStripMenuItem
    Dim WithEvents viewMenu As ToolStripMenuItem
    Dim WithEvents resetViewMenuItem As ToolStripMenuItem
    Dim WithEvents helpMenu As ToolStripMenuItem
    Dim WithEvents aboutMenuItem As ToolStripMenuItem
    Dim WithEvents toolbar As ToolStrip
    Dim WithEvents dimensionLabel As ToolStripLabel
    Dim WithEvents modeLabel As ToolStripLabel
    Dim WithEvents connectionLabel As ToolStripLabel
    Dim WithEvents thresholdLabel As ToolStripLabel
    Dim WithEvents pointSizeLabel As ToolStripLabel
    Dim WithEvents stimStrengthLabel As ToolStripLabel
    Dim WithEvents stimStepsLabel As ToolStripLabel
    Dim WithEvents snapshotButton As ToolStripButton
    Dim WithEvents reloadButton As ToolStripButton
    Dim WithEvents statusBar As StatusStrip
    Friend WithEvents m_canvas As DxScene3DCanvas
    Friend WithEvents m_legend As CheckedListBox
    Friend WithEvents m_gradient As PictureBox
    Friend WithEvents m_details As TextBox
    Friend WithEvents m_replayTrack As TrackBar
    Friend WithEvents m_replaySpeed As NumericUpDown
    Friend WithEvents m_showConnections As ToolStripButton
    Friend WithEvents m_showGround As ToolStripButton
    Friend WithEvents m_stimulateMode As ToolStripButton
    Friend WithEvents m_stimStrengthBox As NumericUpDown
    Friend WithEvents m_stimStepsBox As NumericUpDown
    Friend WithEvents m_holdLabel As ToolStripLabel
    Friend WithEvents m_thresholdBox As NumericUpDown
    Friend WithEvents m_pointSizeBox As NumericUpDown
    Private WithEvents m_dimensionBox As ToolStripComboBox
    Private WithEvents m_connectionBox As ToolStripComboBox
    Private WithEvents m_renderModeBox As ToolStripComboBox
    Private WithEvents m_lineColorBox As ToolStripComboBox
    Friend WithEvents m_progress As ToolStripProgressBar
    Friend WithEvents m_statusText As ToolStripStatusLabel
    Friend WithEvents m_sceneText As ToolStripStatusLabel
    Friend WithEvents m_chartLink As LinkLabel
    Friend WithEvents m_snakeLink As LinkLabel

    ''' <summary>状态栏链接的悬停提示 (LinkLabel 没有 ToolTipText 属性)。</summary>
    Friend WithEvents m_chartTip As ToolTip

    ''' <summary>
    ''' 拖动 / 筛选滑块的防抖定时器 (面板"应用"一次而不是每帧重建)。
    ''' </summary>
    ''' <remarks>
    ''' 必须写全 System.Windows.Forms.Timer：System.Threading 里也有一个同名的 Timer
    ''' (那个在多线程上触发回调，拿来更新界面会直接踩到跨线程访问)。
    ''' </remarks>
    Private WithEvents m_rebuildTimer As System.Windows.Forms.Timer
    Friend WithEvents m_thresholdHost As NumericUpDown
    Friend WithEvents m_pointSizeHost As NumericUpDown
    Friend WithEvents m_stimStrengthHost As NumericUpDown
    Friend WithEvents m_stimStepsHost As NumericUpDown
    Friend WithEvents m_chartLinkHost As LinkLabel
    Friend WithEvents m_snakeLinkHost As LinkLabel
End Class
