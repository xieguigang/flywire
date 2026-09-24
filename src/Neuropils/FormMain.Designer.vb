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

    ' ---- 顶部菜单栏（声明式布局，参照 ResponseChartForm.Designer.vb 的模式）----
    ' 所有控件对象都在 InitializeComponent 中实例化并赋常数字面量；
    ' 事件绑定改由 FormMain.vb 里对应的 Handles 子句完成。
    Dim m_menuStrip As MenuStrip
    Dim m_fileMenu As ToolStripMenuItem
    Dim m_viewMenu As ToolStripMenuItem
    Dim m_helpMenu As ToolStripMenuItem
    Dim m_separator As ToolStripSeparator
    Dim WithEvents m_openItem As ToolStripMenuItem
    Dim WithEvents m_snapshotItem As ToolStripMenuItem
    Dim WithEvents m_exitItem As ToolStripMenuItem
    Dim WithEvents m_resetItem As ToolStripMenuItem
    Dim WithEvents m_aboutItem As ToolStripMenuItem

    ' ---- 状态栏（声明式布局，参照 ResponseChartForm.Designer.vb 的模式）----
    ' 这些字段被其它类 (StimulationExperiment / Snake) 通过 FormMain 实例访问，因此保持 Friend。
    ' 事件绑定改由 FormMain.vb 里对应的 Handles 子句完成。
    Dim m_statusStrip As StatusStrip
    Friend WithEvents m_statusText As ToolStripStatusLabel
    Friend WithEvents m_progress As ToolStripProgressBar
    Friend m_sceneText As ToolStripStatusLabel
    Friend WithEvents m_chartLink As LinkLabel
    Friend WithEvents m_snakeLink As LinkLabel

    ' ---- 工具条（声明式布局，参照 ResponseChartForm.Designer.vb 的模式）----
    ' 被其它类 (StimulationExperiment) 通过 FormMain 实例访问的保持 Friend；
    ' 事件源用 WithEvents 以便 Handles 绑定，替代原 createToolbar 里的 AddHandler。
    Dim m_toolStrip As ToolStrip
    Private WithEvents m_dimensionBox As ToolStripComboBox
    Private WithEvents m_connectionBox As ToolStripComboBox
    Private WithEvents m_lineColorBox As ToolStripComboBox
    Private WithEvents m_renderModeBox As ToolStripComboBox
    Private WithEvents m_thresholdBox As NumericUpDown
    Private WithEvents m_pointSizeBox As NumericUpDown
    Friend WithEvents m_showConnections As ToolStripButton
    Private WithEvents m_showGround As ToolStripButton
    Friend WithEvents m_stimulateMode As ToolStripButton
    Friend m_stimStrengthBox As NumericUpDown
    Friend WithEvents m_stimStepsBox As NumericUpDown
    Friend m_holdLabel As ToolStripLabel
    Private WithEvents m_snapshotButton As ToolStripButton
    Private WithEvents m_reloadButton As ToolStripButton

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        SuspendLayout()
        ' 
        ' FormMain
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1411, 818)
        Name = "FormMain"
        Text = "Neuropils"
        ' 
        ' m_menuStrip
        ' 顶部菜单栏：停靠在窗体顶端，宽度取窗体客户区宽度 (1500, 见 initializeUi 的 ClientSize)，高度 24 为单排菜单的标准高度
        ' 
        m_menuStrip = New MenuStrip()
        m_menuStrip.Dock = DockStyle.Top
        m_menuStrip.Location = New Point(0, 0)
        m_menuStrip.Name = "m_menuStrip"
        m_menuStrip.Size = New Size(1500, 24)
        m_menuStrip.TabIndex = 0
        m_menuStrip.Text = ""
        ' 
        ' m_fileMenu
        ' 
        m_fileMenu = New ToolStripMenuItem()
        m_fileMenu.Name = "m_fileMenu"
        m_fileMenu.Size = New Size(61, 21)
        m_fileMenu.Text = "文件 (&F)"
        ' 
        ' m_openItem
        ' 
        m_openItem = New ToolStripMenuItem()
        m_openItem.Name = "m_openItem"
        m_openItem.Size = New Size(180, 22)
        m_openItem.Text = "打开数据目录 (&O)..."
        ' 
        ' m_snapshotItem
        ' 
        m_snapshotItem = New ToolStripMenuItem()
        m_snapshotItem.Name = "m_snapshotItem"
        m_snapshotItem.Size = New Size(168, 22)
        m_snapshotItem.Text = "保存截图 (&S)..."
        ' 
        ' m_separator
        ' 
        m_separator = New ToolStripSeparator()
        m_separator.Name = "m_separator"
        m_separator.Size = New Size(177, 6)
        ' 
        ' m_exitItem
        ' 
        m_exitItem = New ToolStripMenuItem()
        m_exitItem.Name = "m_exitItem"
        m_exitItem.Size = New Size(120, 22)
        m_exitItem.Text = "退出 (&X)"
        ' 
        ' m_viewMenu
        ' 
        m_viewMenu = New ToolStripMenuItem()
        m_viewMenu.Name = "m_viewMenu"
        m_viewMenu.Size = New Size(60, 21)
        m_viewMenu.Text = "视图 (&V)"
        ' 
        ' m_resetItem
        ' 
        m_resetItem = New ToolStripMenuItem()
        m_resetItem.Name = "m_resetItem"
        m_resetItem.Size = New Size(140, 22)
        m_resetItem.Text = "重置视角 (&R)"
        ' 
        ' m_helpMenu
        ' 
        m_helpMenu = New ToolStripMenuItem()
        m_helpMenu.Name = "m_helpMenu"
        m_helpMenu.Size = New Size(60, 21)
        m_helpMenu.Text = "帮助 (&H)"
        ' 
        ' m_aboutItem
        ' 
        m_aboutItem = New ToolStripMenuItem()
        m_aboutItem.Name = "m_aboutItem"
        m_aboutItem.Size = New Size(160, 22)
        m_aboutItem.Text = "关于数据来源 (&A)"
        ' 
        ' m_menuStrip 容器装配
        ' 
        m_menuStrip.SuspendLayout()
        m_fileMenu.DropDownItems.AddRange(New ToolStripItem() {m_openItem, m_snapshotItem, m_separator, m_exitItem})
        m_viewMenu.DropDownItems.Add(m_resetItem)
        m_helpMenu.DropDownItems.Add(m_aboutItem)
        m_menuStrip.Items.AddRange(New ToolStripItem() {m_fileMenu, m_viewMenu, m_helpMenu})
        m_menuStrip.ResumeLayout(False)
        ' 
        ' m_statusStrip
        ' 状态栏：停靠在窗体底端；宽度取窗体客户区宽度 (1500, 见 initializeUi 的 ClientSize)，
        ' 高度 22 为状态栏标准高度，故 y = 900 - 22 = 878
        ' 
        m_statusStrip = New StatusStrip()
        m_statusStrip.Dock = DockStyle.Bottom
        m_statusStrip.Location = New Point(0, 878)
        m_statusStrip.Name = "m_statusStrip"
        m_statusStrip.Size = New Size(1500, 22)
        m_statusStrip.TabIndex = 1
        ' 
        ' m_statusText
        ' 
        m_statusText = New ToolStripStatusLabel()
        m_statusText.Name = "m_statusText"
        m_statusText.Size = New Size(100, 17)
        m_statusText.Spring = True
        m_statusText.Text = "就绪"
        m_statusText.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_sceneText
        ' 
        m_sceneText = New ToolStripStatusLabel()
        m_sceneText.Name = "m_sceneText"
        m_sceneText.Size = New Size(100, 17)
        m_sceneText.Text = ""
        m_sceneText.BorderSides = ToolStripStatusLabelBorderSides.Left
        ' 
        ' m_progress
        ' 
        m_progress = New ToolStripProgressBar()
        m_progress.Name = "m_progress"
        m_progress.Size = New Size(220, 16)
        m_progress.Visible = False
        m_progress.Style = ProgressBarStyle.Marquee
        ' 
        ' m_chartLink
        ' 
        m_chartLink = New LinkLabel()
        m_chartLink.Name = "m_chartLink"
        m_chartLink.AutoSize = True
        m_chartLink.Enabled = False
        m_chartLink.Size = New Size(56, 17)
        m_chartLink.Text = "响应曲线"
        m_chartLink.LinkBehavior = LinkBehavior.HoverUnderline
        m_chartLink.LinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        m_chartLink.ActiveLinkColor = Color.White
        m_chartLink.VisitedLinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        m_chartLink.DisabledLinkColor = Color.FromArgb(CByte(100), CByte(116), CByte(139))
        m_chartLink.Margin = New Padding(6, 4, 6, 0)
        ' 
        ' m_snakeLink
        ' 
        m_snakeLink = New LinkLabel()
        m_snakeLink.Name = "m_snakeLink"
        m_snakeLink.AutoSize = True
        m_snakeLink.Size = New Size(140, 17)
        m_snakeLink.Text = "果蝇大脑玩贪吃蛇"
        m_snakeLink.LinkBehavior = LinkBehavior.HoverUnderline
        m_snakeLink.LinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        m_snakeLink.ActiveLinkColor = Color.White
        m_snakeLink.VisitedLinkColor = Color.FromArgb(CByte(34), CByte(211), CByte(238))
        m_snakeLink.Margin = New Padding(6, 4, 6, 0)
        ' 
        ' m_statusStrip 容器装配
        ' 
        m_statusStrip.SuspendLayout()
        m_statusStrip.Items.Add(m_statusText)
        m_statusStrip.Items.Add(m_sceneText)
        m_statusStrip.Items.Add(m_progress)
        m_statusStrip.Items.Add(New ToolStripControlHost(m_chartLink) With {.Alignment = ToolStripItemAlignment.Left})
        m_statusStrip.Items.Add(New ToolStripControlHost(m_snakeLink) With {.Alignment = ToolStripItemAlignment.Left})
        m_statusStrip.ResumeLayout(False)
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
        m_thresholdBox = New NumericUpDown()
        m_thresholdBox.Minimum = 1
        m_thresholdBox.Maximum = 100000
        m_thresholdBox.Increment = 10
        m_thresholdBox.Value = m_buildOptions.SynapseThreshold
        m_thresholdBox.Name = "m_thresholdBox"
        m_thresholdBox.Size = New Size(80, 23)
        ' 
        ' m_pointSizeBox
        ' 
        m_pointSizeBox = New NumericUpDown()
        m_pointSizeBox.Minimum = 1
        m_pointSizeBox.Maximum = 12
        m_pointSizeBox.Increment = 1
        m_pointSizeBox.Value = 2
        m_pointSizeBox.Name = "m_pointSizeBox"
        m_pointSizeBox.Size = New Size(56, 23)
        ' 
        ' m_showConnections
        ' 
        m_showConnections = New ToolStripButton()
        m_showConnections.CheckOnClick = True
        m_showConnections.Checked = False
        m_showConnections.Name = "m_showConnections"
        m_showConnections.Size = New Size(75, 22)
        m_showConnections.Text = "显示连接"
        ' 
        ' m_showGround
        ' 
        m_showGround = New ToolStripButton()
        m_showGround.CheckOnClick = True
        m_showGround.Checked = False
        m_showGround.Name = "m_showGround"
        m_showGround.Size = New Size(45, 22)
        m_showGround.Text = "地面"
        ' 
        ' m_snapshotButton
        ' 
        m_snapshotButton = New ToolStripButton()
        m_snapshotButton.Name = "m_snapshotButton"
        m_snapshotButton.Size = New Size(45, 22)
        m_snapshotButton.Text = "截图"
        ' 
        ' m_reloadButton
        ' 
        m_reloadButton = New ToolStripButton()
        m_reloadButton.Name = "m_reloadButton"
        m_reloadButton.Size = New Size(60, 22)
        m_reloadButton.Text = "重新载入"
        ' 
        ' m_stimulateMode
        ' 
        m_stimulateMode = New ToolStripButton()
        m_stimulateMode.CheckOnClick = True
        m_stimulateMode.Checked = False
        m_stimulateMode.Name = "m_stimulateMode"
        m_stimulateMode.Size = New Size(75, 22)
        m_stimulateMode.Text = "电刺激模式"
        m_stimulateMode.ToolTipText = "勾选后：在神经元上按住左键（越久越强），松开即运行一次全脑 SNN 仿真并回放激活过程"
        ' 
        ' m_stimStrengthBox
        ' 
        m_stimStrengthBox = New NumericUpDown()
        m_stimStrengthBox.DecimalPlaces = 1
        m_stimStrengthBox.Minimum = CDec(0.2)
        m_stimStrengthBox.Maximum = CDec(20)
        m_stimStrengthBox.Increment = CDec(0.5)
        m_stimStrengthBox.Value = CDec(1)
        m_stimStrengthBox.Name = "m_stimStrengthBox"
        m_stimStrengthBox.Size = New Size(56, 23)
        ' 
        ' m_stimStepsBox
        ' 
        m_stimStepsBox = New NumericUpDown()
        m_stimStepsBox.Minimum = 5
        m_stimStepsBox.Maximum = 200
        m_stimStepsBox.Increment = 5
        m_stimStepsBox.Value = 30
        m_stimStepsBox.Name = "m_stimStepsBox"
        m_stimStepsBox.Size = New Size(56, 23)
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
        m_toolStrip.SuspendLayout()
        m_toolStrip.Items.Add(New ToolStripLabel("着色:"))
        m_toolStrip.Items.Add(m_dimensionBox)
        m_toolStrip.Items.Add(New ToolStripSeparator())
        m_toolStrip.Items.Add(New ToolStripLabel("模式:"))
        m_toolStrip.Items.Add(m_renderModeBox)
        m_toolStrip.Items.Add(New ToolStripLabel("连接:"))
        m_toolStrip.Items.Add(m_connectionBox)
        m_toolStrip.Items.Add(m_lineColorBox)
        m_toolStrip.Items.Add(New ToolStripLabel("≥突触:"))
        m_toolStrip.Items.Add(New ToolStripControlHost(m_thresholdBox))
        m_toolStrip.Items.Add(New ToolStripSeparator())
        m_toolStrip.Items.Add(New ToolStripLabel("点大小:"))
        m_toolStrip.Items.Add(New ToolStripControlHost(m_pointSizeBox))
        m_toolStrip.Items.Add(m_showConnections)
        m_toolStrip.Items.Add(m_showGround)
        m_toolStrip.Items.Add(New ToolStripSeparator())
        m_toolStrip.Items.Add(m_snapshotButton)
        m_toolStrip.Items.Add(m_reloadButton)
        m_toolStrip.Items.Add(New ToolStripSeparator())
        m_toolStrip.Items.Add(m_stimulateMode)
        m_toolStrip.Items.Add(New ToolStripLabel("强度×"))
        m_toolStrip.Items.Add(New ToolStripControlHost(m_stimStrengthBox))
        m_toolStrip.Items.Add(New ToolStripLabel("仿真步数"))
        m_toolStrip.Items.Add(New ToolStripControlHost(m_stimStepsBox))
        m_toolStrip.Items.Add(m_holdLabel)
        m_toolStrip.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

End Class
