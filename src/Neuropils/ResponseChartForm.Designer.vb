Imports Microsoft.VisualBasic.Drawing.DirectX

''' <summary>
''' <see cref="ResponseChartForm"/> 的界面布局（Windows 窗体设计器维护的声明式代码）。
''' </summary>
''' <remarks>
''' 这里只放"控件怎么摆"的代码：控件实例化、属性赋值、容器装配与事件挂接。
''' 数据装配与绘制逻辑（<c>refreshCategories</c> / <c>rebuildSeries</c> / <c>onRender</c> 等）
''' 仍然留在 <c>ResponseChartForm.vb</c> 中。
''' </remarks>
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class ResponseChartForm
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
        Dim root As New TableLayoutPanel()
        Dim filterBar As New TableLayoutPanel()
        Dim dimensionLabel As New Label()
        Dim modeLabel As New Label()
        Dim aggregationLabel As New Label()
        Dim row3 As New FlowLayoutPanel()
        Dim limitLabel As New Label()
        Dim windowLabel As New Label()
        Dim valuePanel As New TableLayoutPanel()
        Dim buttons As New FlowLayoutPanel()
        Dim selectAll As New Button()
        Dim clearAll As New Button()
        Dim exportImage As New Button()
        Dim canvasPanel As New TableLayoutPanel()

        SuspendLayout()
        ' 
        ' ResponseChartForm
        ' 
        Text = "电刺激响应曲线"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1180, 720)
        MinimumSize = New Size(820, 520)
        BackColor = Color.FromArgb(21, 29, 38)
        ForeColor = Color.FromArgb(226, 232, 240)
        Font = New Font("Segoe UI", 9)
        ' 
        ' root：整体两列两行 —— 左列 (筛选条 + 取值列表)，右列 (画布，跨两行)
        ' 
        root.Dock = DockStyle.Fill
        root.ColumnCount = 2
        root.RowCount = 2
        root.BackColor = BackColor
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 268))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 76))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        ' 
        ' filterBar：顶部 —— 筛选维度 / 响应信号 / 曲线组织 / 上限与平滑窗
        ' 
        filterBar.Dock = DockStyle.Fill
        filterBar.ColumnCount = 2
        filterBar.RowCount = 4
        filterBar.Padding = New Padding(8, 6, 8, 0)
        filterBar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 84))
        filterBar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        ' 
        ' 三个字段标签样式一致
        ' 
        dimensionLabel.Text = "筛选维度"
        dimensionLabel.Dock = DockStyle.Fill
        dimensionLabel.TextAlign = ContentAlignment.MiddleLeft
        dimensionLabel.ForeColor = Color.FromArgb(148, 163, 184)

        modeLabel.Text = "响应信号"
        modeLabel.Dock = DockStyle.Fill
        modeLabel.TextAlign = ContentAlignment.MiddleLeft
        modeLabel.ForeColor = Color.FromArgb(148, 163, 184)

        aggregationLabel.Text = "曲线组织"
        aggregationLabel.Dock = DockStyle.Fill
        aggregationLabel.TextAlign = ContentAlignment.MiddleLeft
        aggregationLabel.ForeColor = Color.FromArgb(148, 163, 184)
        ' 
        ' m_dimensionBox
        ' 
        m_dimensionBox = New ComboBox()
        m_dimensionBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_dimensionBox.Dock = DockStyle.Fill
        m_dimensionBox.Items.AddRange(New Object() {
            "主导脑区", "神经递质", "细胞类型", "分类层级 super_class", "分类层级 class", "分类层级 group"
        })
        m_dimensionBox.SelectedIndex = 0
        AddHandler m_dimensionBox.SelectedIndexChanged, AddressOf onDimensionChanged
        ' 
        ' m_modeBox：下拉项与数据有关，由 bindSignalModes 在数据就绪后填充
        ' 
        m_modeBox = New ComboBox()
        m_modeBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_modeBox.Dock = DockStyle.Fill
        AddHandler m_modeBox.SelectedIndexChanged, AddressOf onOptionChanged
        ' 
        ' m_aggregationBox
        ' 
        m_aggregationBox = New ComboBox()
        m_aggregationBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_aggregationBox.Dock = DockStyle.Fill
        m_aggregationBox.Items.AddRange(New Object() {"逐个神经元", "组均值", "组均值 ± 包络"})
        m_aggregationBox.SelectedIndex = 0
        AddHandler m_aggregationBox.SelectedIndexChanged, AddressOf onOptionChanged
        ' 
        ' row3：曲线上限 + 平滑窗
        ' 
        row3.Dock = DockStyle.Fill
        row3.WrapContents = False
        row3.Margin = New Padding(0)

        limitLabel.Text = "曲线上限"
        limitLabel.AutoSize = True
        limitLabel.TextAlign = ContentAlignment.MiddleLeft
        limitLabel.ForeColor = ForeColor
        limitLabel.Margin = New Padding(0, 5, 6, 0)

        windowLabel.Text = "平滑窗"
        windowLabel.AutoSize = True
        windowLabel.TextAlign = ContentAlignment.MiddleLeft
        windowLabel.ForeColor = ForeColor
        windowLabel.Margin = New Padding(12, 5, 6, 0)

        m_limitBox = New NumericUpDown()
        m_limitBox.Minimum = 1
        m_limitBox.Maximum = 200
        m_limitBox.Value = 24
        m_limitBox.Width = 60
        AddHandler m_limitBox.ValueChanged, AddressOf onOptionChanged

        m_windowBox = New NumericUpDown()
        m_windowBox.Minimum = 1
        m_windowBox.Maximum = 21
        m_windowBox.Value = 3
        m_windowBox.Width = 50
        AddHandler m_windowBox.ValueChanged, AddressOf onOptionChanged

        row3.Controls.Add(limitLabel)
        row3.Controls.Add(m_limitBox)
        row3.Controls.Add(windowLabel)
        row3.Controls.Add(m_windowBox)

        filterBar.Controls.Add(dimensionLabel, 0, 0)
        filterBar.Controls.Add(m_dimensionBox, 1, 0)
        filterBar.Controls.Add(modeLabel, 0, 1)
        filterBar.Controls.Add(m_modeBox, 1, 1)
        filterBar.Controls.Add(aggregationLabel, 0, 2)
        filterBar.Controls.Add(m_aggregationBox, 1, 2)
        filterBar.Controls.Add(row3, 1, 3)
        ' 
        ' valuePanel：左侧 —— 标签取值勾选（勾选 = 只看这些，不勾选 = 全部）
        ' 
        valuePanel.Dock = DockStyle.Fill
        valuePanel.ColumnCount = 1
        valuePanel.RowCount = 4
        valuePanel.Padding = New Padding(8, 0, 4, 8)
        valuePanel.BackColor = BackColor
        valuePanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
        valuePanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30))
        valuePanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        valuePanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 62))

        m_valueHint = New Label()
        m_valueHint.Text = "勾选 = 只看这些；不勾选 = 全部响应神经元"
        m_valueHint.Dock = DockStyle.Fill
        m_valueHint.ForeColor = Color.FromArgb(148, 163, 184)
        m_valueHint.AutoEllipsis = True
        ' 
        ' buttons：全选 / 清空 / 保存图片
        ' 
        buttons.Dock = DockStyle.Fill
        buttons.WrapContents = False
        buttons.Margin = New Padding(0)

        selectAll.Text = "全选"
        selectAll.Width = 62
        selectAll.Height = 24
        selectAll.Margin = New Padding(0, 2, 4, 0)

        clearAll.Text = "清空"
        clearAll.Width = 62
        clearAll.Height = 24
        clearAll.Margin = New Padding(0, 2, 4, 0)

        exportImage.Text = "保存图片"
        exportImage.Width = 84
        exportImage.Height = 24
        exportImage.Margin = New Padding(0, 2, 0, 0)

        AddHandler selectAll.Click, Sub() Call setAllValues(True)
        AddHandler clearAll.Click, Sub() Call setAllValues(False)
        AddHandler exportImage.Click, AddressOf onSaveImage

        buttons.Controls.Add(selectAll)
        buttons.Controls.Add(clearAll)
        buttons.Controls.Add(exportImage)
        ' 
        ' m_values
        ' 
        m_values = New CheckedListBox()
        m_values.Dock = DockStyle.Fill
        m_values.CheckOnClick = True
        m_values.IntegralHeight = False
        m_values.BackColor = Color.FromArgb(15, 22, 30)
        m_values.ForeColor = Color.FromArgb(226, 232, 240)
        m_values.BorderStyle = BorderStyle.FixedSingle
        AddHandler m_values.ItemCheck, AddressOf onValueChecked
        ' 
        ' m_summary
        ' 
        m_summary = New Label()
        m_summary.Dock = DockStyle.Fill
        m_summary.ForeColor = Color.FromArgb(148, 163, 184)
        m_summary.AutoEllipsis = True
        m_summary.Text = ""

        valuePanel.Controls.Add(m_valueHint, 0, 0)
        valuePanel.Controls.Add(buttons, 0, 1)
        valuePanel.Controls.Add(m_values, 0, 2)
        valuePanel.Controls.Add(m_summary, 0, 3)
        ' 
        ' canvasPanel：右侧 —— GPU 画布 + 状态行
        ' 
        canvasPanel.Dock = DockStyle.Fill
        canvasPanel.ColumnCount = 1
        canvasPanel.RowCount = 2
        canvasPanel.Padding = New Padding(0, 6, 8, 8)
        canvasPanel.BackColor = BackColor
        canvasPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        canvasPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 26))
        ' 
        ' m_canvas
        ' 
        m_canvas = New DxCanvas()
        m_canvas.Dock = DockStyle.Fill
        m_canvas.AutoClear = True
        m_canvas.BackgroundColor = m_theme.BackgroundColor
        AddHandler m_canvas.Render, AddressOf onRender
        ' 
        ' m_status
        ' 
        m_status = New Label()
        m_status.Dock = DockStyle.Fill
        m_status.ForeColor = Color.FromArgb(148, 163, 184)
        m_status.AutoEllipsis = True
        m_status.TextAlign = ContentAlignment.MiddleLeft
        m_status.Text = ""

        canvasPanel.Controls.Add(m_canvas, 0, 0)
        canvasPanel.Controls.Add(m_status, 0, 1)

        root.Controls.Add(filterBar, 0, 0)
        root.Controls.Add(valuePanel, 0, 1)
        root.Controls.Add(canvasPanel, 1, 0)
        root.SetRowSpan(canvasPanel, 2)

        Controls.Add(root)

        ResumeLayout(False)
        PerformLayout()
    End Sub

End Class
