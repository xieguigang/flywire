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
        vlabel = New Label()
        replayBottomPanel = New TableLayoutPanel()
        m_replaySpeed = New NumericUpDown()
        m_replayText = New Label()
        sidebarPanel = New TableLayoutPanel()
        legendLabel = New Label()
        m_gradient = New PictureBox()
        m_legend = New CheckedListBox()
        infoLabel = New Label()
        m_details = New TextBox()
        replayLabel = New Label()
        m_replayPanel = New TableLayoutPanel()
        replayButtonsPanel = New FlowLayoutPanel()
        m_replayFirst = New Button()
        m_replayPrev = New Button()
        m_replayPlay = New Button()
        m_replayNext = New Button()
        m_replayLast = New Button()
        m_replayClear = New Button()
        m_replayTrack = New TrackBar()
        m_canvas = New DxScene3DCanvas()
        m_split = New SplitContainer()
        m_toolStrip = New ToolStrip()
        colorLabel = New ToolStripLabel()
        m_dimensionBox = New ToolStripComboBox()
        sep111 = New ToolStripSeparator()
        modeLabel = New ToolStripLabel()
        m_renderModeBox = New ToolStripComboBox()
        connLabel = New ToolStripLabel()
        m_connectionBox = New ToolStripComboBox()
        lineColorLabel = New ToolStripLabel()
        m_lineColorBox = New ToolStripComboBox()
        m_holdLabel = New ToolStripLabel()
        replayBottomPanel.SuspendLayout()
        CType(m_replaySpeed, ComponentModel.ISupportInitialize).BeginInit()
        sidebarPanel.SuspendLayout()
        CType(m_gradient, ComponentModel.ISupportInitialize).BeginInit()
        m_replayPanel.SuspendLayout()
        replayButtonsPanel.SuspendLayout()
        CType(m_replayTrack, ComponentModel.ISupportInitialize).BeginInit()
        CType(m_split, ComponentModel.ISupportInitialize).BeginInit()
        m_split.Panel1.SuspendLayout()
        m_split.Panel2.SuspendLayout()
        m_split.SuspendLayout()
        m_toolStrip.SuspendLayout()
        SuspendLayout()
        ' 
        ' vlabel
        ' 
        vlabel.Dock = DockStyle.Fill
        vlabel.Location = New Point(3, 0)
        vlabel.Name = "vlabel"
        vlabel.Size = New Size(28, 53)
        vlabel.TabIndex = 0
        vlabel.Text = "速度"
        vlabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' replayBottomPanel
        ' 
        replayBottomPanel.ColumnCount = 3
        replayBottomPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 34.0F))
        replayBottomPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 86.0F))
        replayBottomPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        replayBottomPanel.Controls.Add(vlabel, 0, 0)
        replayBottomPanel.Controls.Add(m_replaySpeed, 1, 0)
        replayBottomPanel.Controls.Add(m_replayText, 2, 0)
        replayBottomPanel.Dock = DockStyle.Fill
        replayBottomPanel.Location = New Point(0, 60)
        replayBottomPanel.Margin = New Padding(0)
        replayBottomPanel.Name = "replayBottomPanel"
        replayBottomPanel.RowCount = 1
        replayBottomPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 20.0F))
        replayBottomPanel.Size = New Size(247, 53)
        replayBottomPanel.TabIndex = 2
        ' 
        ' m_replaySpeed
        ' 
        m_replaySpeed.Increment = New Decimal(New Integer() {20, 0, 0, 0})
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
        m_replayText.Size = New Size(121, 53)
        m_replayText.TabIndex = 2
        m_replayText.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' sidebarPanel
        ' 
        sidebarPanel.ColumnCount = 1
        sidebarPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 20.0F))
        sidebarPanel.Controls.Add(legendLabel, 0, 0)
        sidebarPanel.Controls.Add(m_gradient, 0, 1)
        sidebarPanel.Controls.Add(m_legend, 0, 2)
        sidebarPanel.Controls.Add(infoLabel, 0, 3)
        sidebarPanel.Controls.Add(m_details, 0, 4)
        sidebarPanel.Controls.Add(replayLabel, 0, 5)
        sidebarPanel.Controls.Add(m_replayPanel, 0, 6)
        sidebarPanel.Dock = DockStyle.Fill
        sidebarPanel.Location = New Point(0, 0)
        sidebarPanel.Name = "sidebarPanel"
        sidebarPanel.Padding = New Padding(6)
        sidebarPanel.RowCount = 7
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24.0F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 26.0F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 38.0F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24.0F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 34.0F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24.0F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 112.0F))
        sidebarPanel.Size = New Size(259, 875)
        sidebarPanel.TabIndex = 0
        ' 
        ' legendLabel
        ' 
        legendLabel.Dock = DockStyle.Fill
        legendLabel.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        legendLabel.Location = New Point(9, 6)
        legendLabel.Name = "legendLabel"
        legendLabel.Size = New Size(241, 24)
        legendLabel.TabIndex = 0
        legendLabel.Text = "图例 / 筛选 (勾选控制显示)"
        legendLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_gradient
        ' 
        m_gradient.Dock = DockStyle.Fill
        m_gradient.Location = New Point(9, 33)
        m_gradient.Name = "m_gradient"
        m_gradient.Size = New Size(241, 20)
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
        m_legend.Size = New Size(241, 338)
        m_legend.TabIndex = 2
        ' 
        ' infoLabel
        ' 
        infoLabel.Dock = DockStyle.Fill
        infoLabel.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        infoLabel.Location = New Point(9, 400)
        infoLabel.Name = "infoLabel"
        infoLabel.Size = New Size(241, 24)
        infoLabel.TabIndex = 3
        infoLabel.Text = "神经元详情 (点击画布中的点)"
        infoLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_details
        ' 
        m_details.BackColor = Color.FromArgb(CByte(250), CByte(250), CByte(250))
        m_details.Dock = DockStyle.Fill
        m_details.Font = New Font("Consolas", 9.0F)
        m_details.Location = New Point(9, 427)
        m_details.Multiline = True
        m_details.Name = "m_details"
        m_details.ReadOnly = True
        m_details.ScrollBars = ScrollBars.Vertical
        m_details.Size = New Size(241, 302)
        m_details.TabIndex = 4
        ' 
        ' replayLabel
        ' 
        replayLabel.Dock = DockStyle.Fill
        replayLabel.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        replayLabel.Location = New Point(9, 732)
        replayLabel.Name = "replayLabel"
        replayLabel.Size = New Size(241, 24)
        replayLabel.TabIndex = 5
        replayLabel.Text = "电刺激 / 回放"
        replayLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_replayPanel
        ' 
        m_replayPanel.ColumnCount = 1
        m_replayPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 20.0F))
        m_replayPanel.Controls.Add(replayButtonsPanel, 0, 0)
        m_replayPanel.Controls.Add(m_replayTrack, 0, 1)
        m_replayPanel.Controls.Add(replayBottomPanel, 0, 2)
        m_replayPanel.Dock = DockStyle.Fill
        m_replayPanel.Location = New Point(6, 756)
        m_replayPanel.Margin = New Padding(0)
        m_replayPanel.Name = "m_replayPanel"
        m_replayPanel.RowCount = 3
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        m_replayPanel.Size = New Size(247, 113)
        m_replayPanel.TabIndex = 6
        ' 
        ' replayButtonsPanel
        ' 
        replayButtonsPanel.Controls.Add(m_replayFirst)
        replayButtonsPanel.Controls.Add(m_replayPrev)
        replayButtonsPanel.Controls.Add(m_replayPlay)
        replayButtonsPanel.Controls.Add(m_replayNext)
        replayButtonsPanel.Controls.Add(m_replayLast)
        replayButtonsPanel.Controls.Add(m_replayClear)
        replayButtonsPanel.Dock = DockStyle.Fill
        replayButtonsPanel.Location = New Point(0, 0)
        replayButtonsPanel.Margin = New Padding(0)
        replayButtonsPanel.Name = "replayButtonsPanel"
        replayButtonsPanel.Size = New Size(247, 30)
        replayButtonsPanel.TabIndex = 0
        replayButtonsPanel.WrapContents = False
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
        m_replayClear.Text = "清除"
        ' 
        ' m_replayTrack
        ' 
        m_replayTrack.Dock = DockStyle.Fill
        m_replayTrack.Location = New Point(3, 33)
        m_replayTrack.Maximum = 0
        m_replayTrack.Name = "m_replayTrack"
        m_replayTrack.Size = New Size(241, 24)
        m_replayTrack.TabIndex = 1
        m_replayTrack.TickStyle = TickStyle.None
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
        m_canvas.Size = New Size(1237, 875)
        m_canvas.TabIndex = 0
        m_canvas.UseEmbeddedColor = True
        ' 
        ' m_split
        ' 
        m_split.Dock = DockStyle.Fill
        m_split.FixedPanel = FixedPanel.Panel2
        m_split.Location = New Point(0, 25)
        m_split.Name = "m_split"
        ' 
        ' m_split.Panel1
        ' 
        m_split.Panel1.Controls.Add(m_canvas)
        ' 
        ' m_split.Panel2
        ' 
        m_split.Panel2.Controls.Add(sidebarPanel)
        m_split.Size = New Size(1500, 875)
        m_split.SplitterDistance = 1237
        m_split.TabIndex = 1
        ' 
        ' m_toolStrip
        ' 
        m_toolStrip.GripStyle = ToolStripGripStyle.Hidden
        m_toolStrip.Items.AddRange(New ToolStripItem() {colorLabel, m_dimensionBox, sep111, modeLabel, m_renderModeBox, connLabel, m_connectionBox, lineColorLabel, m_lineColorBox, m_holdLabel})
        m_toolStrip.Location = New Point(0, 0)
        m_toolStrip.Name = "m_toolStrip"
        m_toolStrip.Size = New Size(1500, 25)
        m_toolStrip.TabIndex = 2
        ' 
        ' colorLabel
        ' 
        colorLabel.Name = "colorLabel"
        colorLabel.Size = New Size(85, 22)
        colorLabel.Text = "神经元着色："
        ' 
        ' m_dimensionBox
        ' 
        m_dimensionBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_dimensionBox.Items.AddRange(New Object() {"主导脑区 (neuropil)", "神经递质", "细胞类型", "分类层级", "仿真活跃度"})
        m_dimensionBox.Name = "m_dimensionBox"
        m_dimensionBox.Size = New Size(150, 25)
        m_dimensionBox.Text = "主导脑区 (neuropil)"
        ' 
        ' sep111
        ' 
        sep111.Name = "sep111"
        sep111.Size = New Size(6, 25)
        ' 
        ' modeLabel
        ' 
        modeLabel.Name = "modeLabel"
        modeLabel.Size = New Size(46, 22)
        modeLabel.Text = "模式："
        ' 
        ' m_renderModeBox
        ' 
        m_renderModeBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_renderModeBox.Items.AddRange(New Object() {"点云", "线框", "实体"})
        m_renderModeBox.Name = "m_renderModeBox"
        m_renderModeBox.Size = New Size(110, 25)
        m_renderModeBox.Text = "点云"
        ' 
        ' connLabel
        ' 
        connLabel.Name = "connLabel"
        connLabel.Size = New Size(46, 22)
        connLabel.Text = "连接："
        ' 
        ' m_connectionBox
        ' 
        m_connectionBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_connectionBox.Items.AddRange(New Object() {"连接 (逐条)", "脑区宏连接", "选中神经元的连接"})
        m_connectionBox.Name = "m_connectionBox"
        m_connectionBox.Size = New Size(150, 25)
        m_connectionBox.Text = "连接 (逐条)"
        ' 
        ' lineColorLabel
        ' 
        lineColorLabel.Name = "lineColorLabel"
        lineColorLabel.Size = New Size(111, 22)
        lineColorLabel.Text = "神经元链接颜色："
        ' 
        ' m_lineColorBox
        ' 
        m_lineColorBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_lineColorBox.Items.AddRange(New Object() {"连线: 前突触颜色", "连线: 递质类型", "连线: 单色"})
        m_lineColorBox.Name = "m_lineColorBox"
        m_lineColorBox.Size = New Size(130, 25)
        m_lineColorBox.Text = "连线: 前突触颜色"
        ' 
        ' m_holdLabel
        ' 
        m_holdLabel.Name = "m_holdLabel"
        m_holdLabel.Size = New Size(0, 22)
        ' 
        ' PageFlywireCanvas
        ' 
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1500, 900)
        Controls.Add(m_split)
        Controls.Add(m_toolStrip)
        DockAreas = Microsoft.VisualStudio.WinForms.Docking.DockAreas.Float Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockLeft Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockRight Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockTop Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockBottom Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.Document
        DoubleBuffered = True
        MinimumSize = New Size(900, 600)
        Name = "PageFlywireCanvas"
        ShowHint = Microsoft.VisualStudio.WinForms.Docking.DockState.Unknown
        StartPosition = FormStartPosition.CenterScreen
        TabPageContextMenuStrip = DockContextMenuStrip1
        Text = "Neuropils - Drosophila brain 3D viewer"
        replayBottomPanel.ResumeLayout(False)
        CType(m_replaySpeed, ComponentModel.ISupportInitialize).EndInit()
        sidebarPanel.ResumeLayout(False)
        sidebarPanel.PerformLayout()
        CType(m_gradient, ComponentModel.ISupportInitialize).EndInit()
        m_replayPanel.ResumeLayout(False)
        m_replayPanel.PerformLayout()
        replayButtonsPanel.ResumeLayout(False)
        CType(m_replayTrack, ComponentModel.ISupportInitialize).EndInit()
        m_split.Panel1.ResumeLayout(False)
        m_split.Panel2.ResumeLayout(False)
        CType(m_split, ComponentModel.ISupportInitialize).EndInit()
        m_split.ResumeLayout(False)
        m_toolStrip.ResumeLayout(False)
        m_toolStrip.PerformLayout()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Dim colorLabel As ToolStripLabel
    Dim modeLabel As ToolStripLabel
    Dim connLabel As ToolStripLabel
    Dim lineColorLabel As ToolStripLabel
    Dim sep111 As ToolStripSeparator

End Class
