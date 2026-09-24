Imports Galaxy.Workbench.DockDocument

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class FormChartData
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(FormChartData))
        TableLayoutPanel1 = New TableLayoutPanel()
        filterBar = New TableLayoutPanel()
        dimensionLabel = New Label()
        m_dimensionBox = New ComboBox()
        modeLabel = New Label()
        m_modeBox = New ComboBox()
        aggregationLabel = New Label()
        m_aggregationBox = New ComboBox()
        row3 = New FlowLayoutPanel()
        limitLabel = New Label()
        m_limitBox = New NumericUpDown()
        windowLabel = New Label()
        m_windowBox = New NumericUpDown()
        valuePanel = New TableLayoutPanel()
        m_valueHint = New Label()
        buttons = New FlowLayoutPanel()
        selectAll = New Button()
        clearAll = New Button()
        exportImage = New Button()
        m_values = New CheckedListBox()
        m_summary = New Label()
        TableLayoutPanel1.SuspendLayout()
        filterBar.SuspendLayout()
        row3.SuspendLayout()
        CType(m_limitBox, ComponentModel.ISupportInitialize).BeginInit()
        CType(m_windowBox, ComponentModel.ISupportInitialize).BeginInit()
        valuePanel.SuspendLayout()
        buttons.SuspendLayout()
        SuspendLayout()
        ' 
        ' TableLayoutPanel1
        ' 
        TableLayoutPanel1.ColumnCount = 1
        TableLayoutPanel1.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50F))
        TableLayoutPanel1.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50F))
        TableLayoutPanel1.Controls.Add(valuePanel, 0, 1)
        TableLayoutPanel1.Controls.Add(filterBar, 0, 0)
        TableLayoutPanel1.Dock = DockStyle.Fill
        TableLayoutPanel1.Location = New Point(0, 0)
        TableLayoutPanel1.Name = "TableLayoutPanel1"
        TableLayoutPanel1.RowCount = 2
        TableLayoutPanel1.RowStyles.Add(New RowStyle(SizeType.Percent, 12.7882595F))
        TableLayoutPanel1.RowStyles.Add(New RowStyle(SizeType.Percent, 87.21174F))
        TableLayoutPanel1.Size = New Size(379, 954)
        TableLayoutPanel1.TabIndex = 0
        ' 
        ' filterBar
        ' 
        filterBar.ColumnCount = 2
        filterBar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 84F))
        filterBar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100F))
        filterBar.Controls.Add(dimensionLabel, 0, 0)
        filterBar.Controls.Add(m_dimensionBox, 1, 0)
        filterBar.Controls.Add(modeLabel, 0, 1)
        filterBar.Controls.Add(m_modeBox, 1, 1)
        filterBar.Controls.Add(aggregationLabel, 0, 2)
        filterBar.Controls.Add(m_aggregationBox, 1, 2)
        filterBar.Controls.Add(row3, 1, 3)
        filterBar.Dock = DockStyle.Fill
        filterBar.Location = New Point(3, 3)
        filterBar.Name = "filterBar"
        filterBar.Padding = New Padding(8, 6, 8, 0)
        filterBar.RowCount = 4
        filterBar.RowStyles.Add(New RowStyle(SizeType.Absolute, 21F))
        filterBar.RowStyles.Add(New RowStyle(SizeType.Absolute, 19F))
        filterBar.RowStyles.Add(New RowStyle(SizeType.Absolute, 32F))
        filterBar.RowStyles.Add(New RowStyle(SizeType.Absolute, 8F))
        filterBar.Size = New Size(373, 116)
        filterBar.TabIndex = 1
        ' 
        ' dimensionLabel
        ' 
        dimensionLabel.Dock = DockStyle.Fill
        dimensionLabel.ForeColor = Color.FromArgb(CByte(148), CByte(163), CByte(184))
        dimensionLabel.Location = New Point(11, 6)
        dimensionLabel.Name = "dimensionLabel"
        dimensionLabel.Size = New Size(78, 21)
        dimensionLabel.TabIndex = 0
        dimensionLabel.Text = "筛选维度"
        dimensionLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_dimensionBox
        ' 
        m_dimensionBox.Dock = DockStyle.Fill
        m_dimensionBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_dimensionBox.Items.AddRange(New Object() {"主导脑区", "神经递质", "细胞类型", "分类层级 super_class", "分类层级 class", "分类层级 group"})
        m_dimensionBox.Location = New Point(95, 9)
        m_dimensionBox.Name = "m_dimensionBox"
        m_dimensionBox.Size = New Size(267, 23)
        m_dimensionBox.TabIndex = 1
        ' 
        ' modeLabel
        ' 
        modeLabel.Dock = DockStyle.Fill
        modeLabel.ForeColor = Color.FromArgb(CByte(148), CByte(163), CByte(184))
        modeLabel.Location = New Point(11, 27)
        modeLabel.Name = "modeLabel"
        modeLabel.Size = New Size(78, 19)
        modeLabel.TabIndex = 2
        modeLabel.Text = "响应信号"
        modeLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_modeBox
        ' 
        m_modeBox.Dock = DockStyle.Fill
        m_modeBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_modeBox.Location = New Point(95, 30)
        m_modeBox.Name = "m_modeBox"
        m_modeBox.Size = New Size(267, 23)
        m_modeBox.TabIndex = 3
        ' 
        ' aggregationLabel
        ' 
        aggregationLabel.Dock = DockStyle.Fill
        aggregationLabel.ForeColor = Color.FromArgb(CByte(148), CByte(163), CByte(184))
        aggregationLabel.Location = New Point(11, 46)
        aggregationLabel.Name = "aggregationLabel"
        aggregationLabel.Size = New Size(78, 32)
        aggregationLabel.TabIndex = 4
        aggregationLabel.Text = "曲线组织"
        aggregationLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_aggregationBox
        ' 
        m_aggregationBox.Dock = DockStyle.Fill
        m_aggregationBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_aggregationBox.Items.AddRange(New Object() {"逐个神经元", "组均值", "组均值 ± 包络"})
        m_aggregationBox.Location = New Point(95, 49)
        m_aggregationBox.Name = "m_aggregationBox"
        m_aggregationBox.Size = New Size(267, 23)
        m_aggregationBox.TabIndex = 5
        ' 
        ' row3
        ' 
        row3.Controls.Add(limitLabel)
        row3.Controls.Add(m_limitBox)
        row3.Controls.Add(windowLabel)
        row3.Controls.Add(m_windowBox)
        row3.Dock = DockStyle.Fill
        row3.Location = New Point(92, 78)
        row3.Margin = New Padding(0)
        row3.Name = "row3"
        row3.Size = New Size(273, 38)
        row3.TabIndex = 6
        row3.WrapContents = False
        ' 
        ' limitLabel
        ' 
        limitLabel.AutoSize = True
        limitLabel.ForeColor = SystemColors.ControlText
        limitLabel.Location = New Point(0, 5)
        limitLabel.Margin = New Padding(0, 5, 6, 0)
        limitLabel.Name = "limitLabel"
        limitLabel.Size = New Size(59, 15)
        limitLabel.TabIndex = 0
        limitLabel.Text = "曲线上限"
        limitLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_limitBox
        ' 
        m_limitBox.Location = New Point(68, 3)
        m_limitBox.Maximum = New Decimal(New Integer() {200, 0, 0, 0})
        m_limitBox.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        m_limitBox.Name = "m_limitBox"
        m_limitBox.Size = New Size(60, 23)
        m_limitBox.TabIndex = 1
        m_limitBox.Value = New Decimal(New Integer() {24, 0, 0, 0})
        ' 
        ' windowLabel
        ' 
        windowLabel.AutoSize = True
        windowLabel.ForeColor = SystemColors.ControlText
        windowLabel.Location = New Point(143, 5)
        windowLabel.Margin = New Padding(12, 5, 6, 0)
        windowLabel.Name = "windowLabel"
        windowLabel.Size = New Size(46, 15)
        windowLabel.TabIndex = 2
        windowLabel.Text = "平滑窗"
        windowLabel.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' m_windowBox
        ' 
        m_windowBox.Location = New Point(198, 3)
        m_windowBox.Maximum = New Decimal(New Integer() {21, 0, 0, 0})
        m_windowBox.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        m_windowBox.Name = "m_windowBox"
        m_windowBox.Size = New Size(50, 23)
        m_windowBox.TabIndex = 3
        m_windowBox.Value = New Decimal(New Integer() {3, 0, 0, 0})
        ' 
        ' valuePanel
        ' 
        valuePanel.BackColor = SystemColors.Control
        valuePanel.ColumnCount = 1
        valuePanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 20F))
        valuePanel.Controls.Add(m_valueHint, 0, 0)
        valuePanel.Controls.Add(buttons, 0, 1)
        valuePanel.Controls.Add(m_values, 0, 2)
        valuePanel.Controls.Add(m_summary, 0, 3)
        valuePanel.Dock = DockStyle.Fill
        valuePanel.Location = New Point(3, 125)
        valuePanel.Name = "valuePanel"
        valuePanel.Padding = New Padding(8, 0, 4, 8)
        valuePanel.RowCount = 4
        valuePanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 34F))
        valuePanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30F))
        valuePanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100F))
        valuePanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 62F))
        valuePanel.Size = New Size(373, 826)
        valuePanel.TabIndex = 2
        ' 
        ' m_valueHint
        ' 
        m_valueHint.AutoEllipsis = True
        m_valueHint.Dock = DockStyle.Fill
        m_valueHint.ForeColor = Color.FromArgb(CByte(148), CByte(163), CByte(184))
        m_valueHint.Location = New Point(11, 0)
        m_valueHint.Name = "m_valueHint"
        m_valueHint.Size = New Size(355, 34)
        m_valueHint.TabIndex = 0
        m_valueHint.Text = "勾选 = 只看这些；不勾选 = 全部响应神经元"
        ' 
        ' buttons
        ' 
        buttons.Controls.Add(selectAll)
        buttons.Controls.Add(clearAll)
        buttons.Controls.Add(exportImage)
        buttons.Dock = DockStyle.Fill
        buttons.Location = New Point(8, 34)
        buttons.Margin = New Padding(0)
        buttons.Name = "buttons"
        buttons.Size = New Size(361, 30)
        buttons.TabIndex = 1
        buttons.WrapContents = False
        ' 
        ' selectAll
        ' 
        selectAll.ForeColor = Color.Black
        selectAll.Location = New Point(0, 2)
        selectAll.Margin = New Padding(0, 2, 4, 0)
        selectAll.Name = "selectAll"
        selectAll.Size = New Size(62, 24)
        selectAll.TabIndex = 0
        selectAll.Text = "全选"
        ' 
        ' clearAll
        ' 
        clearAll.ForeColor = Color.Black
        clearAll.Location = New Point(66, 2)
        clearAll.Margin = New Padding(0, 2, 4, 0)
        clearAll.Name = "clearAll"
        clearAll.Size = New Size(62, 24)
        clearAll.TabIndex = 1
        clearAll.Text = "清空"
        ' 
        ' exportImage
        ' 
        exportImage.ForeColor = Color.Black
        exportImage.Location = New Point(132, 2)
        exportImage.Margin = New Padding(0, 2, 0, 0)
        exportImage.Name = "exportImage"
        exportImage.Size = New Size(84, 24)
        exportImage.TabIndex = 2
        exportImage.Text = "保存图片"
        ' 
        ' m_values
        ' 
        m_values.BackColor = Color.FromArgb(CByte(15), CByte(22), CByte(30))
        m_values.BorderStyle = BorderStyle.FixedSingle
        m_values.CheckOnClick = True
        m_values.Dock = DockStyle.Fill
        m_values.ForeColor = Color.FromArgb(CByte(226), CByte(232), CByte(240))
        m_values.IntegralHeight = False
        m_values.Location = New Point(11, 67)
        m_values.Name = "m_values"
        m_values.Size = New Size(355, 686)
        m_values.TabIndex = 2
        ' 
        ' m_summary
        ' 
        m_summary.AutoEllipsis = True
        m_summary.Dock = DockStyle.Fill
        m_summary.ForeColor = Color.FromArgb(CByte(148), CByte(163), CByte(184))
        m_summary.Location = New Point(11, 756)
        m_summary.Name = "m_summary"
        m_summary.Size = New Size(355, 62)
        m_summary.TabIndex = 3
        ' 
        ' FormChartData
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(379, 954)
        Controls.Add(TableLayoutPanel1)
        Icon = CType(resources.GetObject("$this.Icon"), Icon)
        Name = "FormChartData"
        Text = "选择曲线数据"
        TableLayoutPanel1.ResumeLayout(False)
        filterBar.ResumeLayout(False)
        row3.ResumeLayout(False)
        row3.PerformLayout()
        CType(m_limitBox, ComponentModel.ISupportInitialize).EndInit()
        CType(m_windowBox, ComponentModel.ISupportInitialize).EndInit()
        valuePanel.ResumeLayout(False)
        buttons.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents TableLayoutPanel1 As TableLayoutPanel
    Private WithEvents filterBar As TableLayoutPanel
    Private WithEvents dimensionLabel As Label
    Private WithEvents m_dimensionBox As ComboBox
    Private WithEvents modeLabel As Label
    Private WithEvents m_modeBox As ComboBox
    Private WithEvents aggregationLabel As Label
    Private WithEvents m_aggregationBox As ComboBox
    Private WithEvents row3 As FlowLayoutPanel
    Private WithEvents limitLabel As Label
    Private WithEvents m_limitBox As NumericUpDown
    Private WithEvents windowLabel As Label
    Private WithEvents m_windowBox As NumericUpDown
    Private WithEvents valuePanel As TableLayoutPanel
    Private WithEvents m_valueHint As Label
    Private WithEvents buttons As FlowLayoutPanel
    Private WithEvents selectAll As Button
    Private WithEvents clearAll As Button
    Private WithEvents exportImage As Button
    Private WithEvents m_values As CheckedListBox
    Private WithEvents m_summary As Label
End Class
