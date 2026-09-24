Imports Galaxy.Workbench.DockDocument

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class FormNeuronMap
    Inherits ToolWindow

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
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
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
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
        replayBottomPanel = New TableLayoutPanel()
        vlabel = New Label()
        m_replaySpeed = New NumericUpDown()
        m_replayText = New Label()
        sidebarPanel.SuspendLayout()
        CType(m_gradient, ComponentModel.ISupportInitialize).BeginInit()
        m_replayPanel.SuspendLayout()
        replayButtonsPanel.SuspendLayout()
        CType(m_replayTrack, ComponentModel.ISupportInitialize).BeginInit()
        replayBottomPanel.SuspendLayout()
        CType(m_replaySpeed, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' sidebarPanel
        ' 
        sidebarPanel.ColumnCount = 1
        sidebarPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 20F))
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
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 26F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 38F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 34F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24F))
        sidebarPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 112F))
        sidebarPanel.Size = New Size(444, 889)
        sidebarPanel.TabIndex = 1
        ' 
        ' legendLabel
        ' 
        legendLabel.Dock = DockStyle.Fill
        legendLabel.Font = New Font("Segoe UI", 9F, FontStyle.Bold)
        legendLabel.Location = New Point(9, 6)
        legendLabel.Name = "legendLabel"
        legendLabel.Size = New Size(426, 24)
        legendLabel.TabIndex = 0
        legendLabel.Text = "图例 / 筛选 (勾选控制显示)"
        legendLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_gradient
        ' 
        m_gradient.Dock = DockStyle.Fill
        m_gradient.Location = New Point(9, 33)
        m_gradient.Name = "m_gradient"
        m_gradient.Size = New Size(426, 20)
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
        m_legend.Size = New Size(426, 346)
        m_legend.TabIndex = 2
        ' 
        ' infoLabel
        ' 
        infoLabel.Dock = DockStyle.Fill
        infoLabel.Font = New Font("Segoe UI", 9F, FontStyle.Bold)
        infoLabel.Location = New Point(9, 408)
        infoLabel.Name = "infoLabel"
        infoLabel.Size = New Size(426, 24)
        infoLabel.TabIndex = 3
        infoLabel.Text = "神经元详情 (点击画布中的点)"
        infoLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_details
        ' 
        m_details.BackColor = Color.FromArgb(CByte(250), CByte(250), CByte(250))
        m_details.Dock = DockStyle.Fill
        m_details.Font = New Font("Consolas", 9F)
        m_details.Location = New Point(9, 435)
        m_details.Multiline = True
        m_details.Name = "m_details"
        m_details.ReadOnly = True
        m_details.ScrollBars = ScrollBars.Vertical
        m_details.Size = New Size(426, 308)
        m_details.TabIndex = 4
        ' 
        ' replayLabel
        ' 
        replayLabel.Dock = DockStyle.Fill
        replayLabel.Font = New Font("Segoe UI", 9F, FontStyle.Bold)
        replayLabel.Location = New Point(9, 746)
        replayLabel.Name = "replayLabel"
        replayLabel.Size = New Size(426, 24)
        replayLabel.TabIndex = 5
        replayLabel.Text = "电刺激 / 回放"
        replayLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_replayPanel
        ' 
        m_replayPanel.ColumnCount = 1
        m_replayPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 20F))
        m_replayPanel.Controls.Add(replayButtonsPanel, 0, 0)
        m_replayPanel.Controls.Add(m_replayTrack, 0, 1)
        m_replayPanel.Controls.Add(replayBottomPanel, 0, 2)
        m_replayPanel.Dock = DockStyle.Fill
        m_replayPanel.Location = New Point(6, 770)
        m_replayPanel.Margin = New Padding(0)
        m_replayPanel.Name = "m_replayPanel"
        m_replayPanel.RowCount = 3
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30F))
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30F))
        m_replayPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100F))
        m_replayPanel.Size = New Size(432, 113)
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
        replayButtonsPanel.Size = New Size(432, 30)
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
        m_replayTrack.Size = New Size(426, 24)
        m_replayTrack.TabIndex = 1
        m_replayTrack.TickStyle = TickStyle.None
        ' 
        ' replayBottomPanel
        ' 
        replayBottomPanel.ColumnCount = 3
        replayBottomPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 34F))
        replayBottomPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 86F))
        replayBottomPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100F))
        replayBottomPanel.Controls.Add(vlabel, 0, 0)
        replayBottomPanel.Controls.Add(m_replaySpeed, 1, 0)
        replayBottomPanel.Controls.Add(m_replayText, 2, 0)
        replayBottomPanel.Dock = DockStyle.Fill
        replayBottomPanel.Location = New Point(0, 60)
        replayBottomPanel.Margin = New Padding(0)
        replayBottomPanel.Name = "replayBottomPanel"
        replayBottomPanel.RowCount = 1
        replayBottomPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 20F))
        replayBottomPanel.Size = New Size(432, 53)
        replayBottomPanel.TabIndex = 2
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
        m_replayText.Size = New Size(306, 53)
        m_replayText.TabIndex = 2
        m_replayText.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' FormNeuronMap
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(444, 889)
        Controls.Add(sidebarPanel)
        Name = "FormNeuronMap"
        Text = "Neuron Map"
        sidebarPanel.ResumeLayout(False)
        sidebarPanel.PerformLayout()
        CType(m_gradient, ComponentModel.ISupportInitialize).EndInit()
        m_replayPanel.ResumeLayout(False)
        m_replayPanel.PerformLayout()
        replayButtonsPanel.ResumeLayout(False)
        CType(m_replayTrack, ComponentModel.ISupportInitialize).EndInit()
        replayBottomPanel.ResumeLayout(False)
        CType(m_replaySpeed, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
    End Sub

    Private WithEvents sidebarPanel As TableLayoutPanel
    Private WithEvents legendLabel As Label
    Friend WithEvents m_gradient As PictureBox
    Private WithEvents m_legend As CheckedListBox
    Private WithEvents infoLabel As Label
    Friend WithEvents m_details As TextBox
    Private WithEvents replayLabel As Label
    Friend WithEvents m_replayPanel As TableLayoutPanel
    Private WithEvents replayButtonsPanel As FlowLayoutPanel
    Friend WithEvents m_replayFirst As Button
    Friend WithEvents m_replayPrev As Button
    Friend WithEvents m_replayPlay As Button
    Friend WithEvents m_replayNext As Button
    Friend WithEvents m_replayLast As Button
    Friend WithEvents m_replayClear As Button
    Friend WithEvents m_replayTrack As TrackBar
    Private WithEvents replayBottomPanel As TableLayoutPanel
    Private WithEvents vlabel As Label
    Friend WithEvents m_replaySpeed As NumericUpDown
    Friend WithEvents m_replayText As Label
End Class
