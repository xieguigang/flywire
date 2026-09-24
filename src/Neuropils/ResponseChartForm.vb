Imports Microsoft.VisualBasic.Data.Plots
Imports Microsoft.VisualBasic.Drawing.DirectX
Imports Neuropils.Analysis
Imports Neuropils.Data

''' <summary>
''' 电刺激响应曲线窗口：把一次刺激实验记录下来的响应结果画成曲线。
''' </summary>
''' <remarks>
''' <b>横轴</b>是时间步，<b>纵轴</b>是神经元的响应电信号强度（默认取触发前膜电位）。
''' 曲线可以逐神经元画，也可以按「脑区 / 神经递质 / 细胞类型 / 分类层级」筛选出的
''' 神经元集合聚合后画（组均值 ± 包络）。
''' 
''' 曲线<b>直接绘制在 <see cref="DxCanvas"/> 控件上</b>（Direct2D/D3D11 GPU 画布）：
''' <c>LinePlot</c> 通过注入 <c>IGraphics</c> 的方式使用控件自己的绘图设备，
''' 中间不需要位图，也不存在"先画到 Bitmap 再贴到控件"的拷贝。
''' 
''' 数据来源是<b>记录下来的响应结果</b>：会话内刚跑完的结果直接画，
''' 也可以随时打开任意一次历史实验的记录目录。
''' </remarks>
Public Class ResponseChartForm
    Inherits Form

#Region "fields"

    Private ReadOnly m_data As ResponseDataset
    Private ReadOnly m_dataset As BrainDataset
    Private ReadOnly m_theme As PlotTheme

    Private m_canvas As DxCanvas
    Private m_dimensionBox As ComboBox
    Private m_modeBox As ComboBox
    Private m_aggregationBox As ComboBox
    Private m_limitBox As NumericUpDown
    Private m_windowBox As NumericUpDown
    Private m_values As CheckedListBox
    Private m_valueHint As Label
    Private m_status As Label
    Private m_summary As Label

    Private m_series As List(Of Series)
    Private m_description As String = ""
    Private m_loading As Boolean

#End Region

    ''' <summary>
    ''' 构造响应曲线窗口。
    ''' </summary>
    ''' <param name="data">响应数据（会话内结果或落盘记录）</param>
    ''' <param name="dataset">数据集（提供脑区 / 递质等标签）</param>
    Public Sub New(data As ResponseDataset, dataset As BrainDataset)
        If data Is Nothing Then Throw New ArgumentNullException(NameOf(data))

        m_data = data
        m_dataset = dataset
        m_theme = PlotTheme.Dark()

        Call PlotRuntime.EnsureRegistered()

        Call initializeUi()
        Call refreshCategories()
        Call rebuildSeries()
    End Sub

#Region "界面装配"

    Private Sub initializeUi()
        Me.Text = "电刺激响应曲线"
        Me.StartPosition = FormStartPosition.CenterParent
        Me.Size = New Size(1180, 720)
        Me.MinimumSize = New Size(820, 520)
        Me.BackColor = Color.FromArgb(21, 29, 38)
        Me.ForeColor = Color.FromArgb(226, 232, 240)
        Me.Font = New Font("Segoe UI", 9)

        Dim root As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 2,
            .BackColor = Me.BackColor
        }

        Call root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 268))
        Call root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        Call root.RowStyles.Add(New RowStyle(SizeType.Absolute, 76))
        Call root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        Call root.Controls.Add(createFilterBar(), 0, 0)
        Call root.Controls.Add(createValuePanel(), 0, 1)
        Call root.Controls.Add(createCanvas(), 1, 0)
        Call root.SetRowSpan(root.GetControlFromPosition(1, 0), 2)

        Me.Controls.Add(root)
    End Sub

    ''' <summary>顶部：信号口径 / 组织方式 / 曲线上限。</summary>
    Private Function createFilterBar() As Control
        Dim panel As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 4,
            .Padding = New Padding(8, 6, 8, 0)
        }

        Call panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 84))
        Call panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

        m_dimensionBox = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
        Call m_dimensionBox.Items.AddRange(New Object() {
            "主导脑区", "神经递质", "细胞类型", "分类层级 super_class", "分类层级 class", "分类层级 group"
        })
        m_dimensionBox.SelectedIndex = 0
        AddHandler m_dimensionBox.SelectedIndexChanged, AddressOf onDimensionChanged

        ' 只列出当前数据真正可用的口径：没有分析回放（膜电位）时就不要给出这个选项
        m_modeBox = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}

        For Each mode As ResponseSignalMode In m_data.AvailableModes
            Call m_modeBox.Items.Add(ResponseCurveBuilder.DescribeMode(mode))
        Next

        m_modeBox.SelectedIndex = 0
        AddHandler m_modeBox.SelectedIndexChanged, AddressOf onOptionChanged

        m_aggregationBox = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
        Call m_aggregationBox.Items.AddRange(New Object() {"逐个神经元", "组均值", "组均值 ± 包络"})
        m_aggregationBox.SelectedIndex = 0
        AddHandler m_aggregationBox.SelectedIndexChanged, AddressOf onOptionChanged

        Call panel.Controls.Add(newFieldLabel("筛选维度"), 0, 0)
        Call panel.Controls.Add(m_dimensionBox, 1, 0)
        Call panel.Controls.Add(newFieldLabel("响应信号"), 0, 1)
        Call panel.Controls.Add(m_modeBox, 1, 1)
        Call panel.Controls.Add(newFieldLabel("曲线组织"), 0, 2)

        Dim row3 As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False, .Margin = New Padding(0)}
        Dim limitLabel As New Label With {
            .Text = "曲线上限", .AutoSize = True, .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Me.ForeColor, .Margin = New Padding(0, 5, 6, 0)
        }
        Dim windowLabel As New Label With {
            .Text = "平滑窗", .AutoSize = True, .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Me.ForeColor, .Margin = New Padding(12, 5, 6, 0)
        }

        m_limitBox = New NumericUpDown With {.Minimum = 1, .Maximum = 200, .Value = 24, .Width = 60}
        AddHandler m_limitBox.ValueChanged, AddressOf onOptionChanged

        m_windowBox = New NumericUpDown With {.Minimum = 1, .Maximum = 21, .Value = 3, .Width = 50}
        AddHandler m_windowBox.ValueChanged, AddressOf onOptionChanged

        Call row3.Controls.Add(limitLabel)
        Call row3.Controls.Add(m_limitBox)
        Call row3.Controls.Add(windowLabel)
        Call row3.Controls.Add(m_windowBox)

        Call panel.Controls.Add(m_aggregationBox, 1, 2)
        Call panel.Controls.Add(row3, 1, 3)

        Return panel
    End Function

    Private Function newFieldLabel(text As String) As Label
        Return New Label With {
            .Text = text, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.FromArgb(148, 163, 184)
        }
    End Function

    ''' <summary>左侧：标签取值勾选（勾选 = 只看这些，不勾选 = 全部）。</summary>
    Private Function createValuePanel() As Control
        Dim panel As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(8, 0, 4, 8),
            .BackColor = Me.BackColor
        }

        Call panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
        Call panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30))
        Call panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Call panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 62))

        m_valueHint = New Label With {
            .Text = "勾选 = 只看这些；不勾选 = 全部响应神经元",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(148, 163, 184),
            .AutoEllipsis = True
        }

        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False, .Margin = New Padding(0)}
        Dim selectAll As New Button With {.Text = "全选", .Width = 62, .Height = 24, .Margin = New Padding(0, 2, 4, 0)}
        Dim clearAll As New Button With {.Text = "清空", .Width = 62, .Height = 24, .Margin = New Padding(0, 2, 4, 0)}
        Dim export As New Button With {.Text = "保存图片", .Width = 84, .Height = 24, .Margin = New Padding(0, 2, 0, 0)}

        AddHandler selectAll.Click, Sub() Call setAllValues(True)
        AddHandler clearAll.Click, Sub() Call setAllValues(False)
        AddHandler export.Click, AddressOf onSaveImage

        Call buttons.Controls.Add(selectAll)
        Call buttons.Controls.Add(clearAll)
        Call buttons.Controls.Add(export)

        m_values = New CheckedListBox With {
            .Dock = DockStyle.Fill,
            .CheckOnClick = True,
            .IntegralHeight = False,
            .BackColor = Color.FromArgb(15, 22, 30),
            .ForeColor = Color.FromArgb(226, 232, 240),
            .BorderStyle = BorderStyle.FixedSingle
        }
        AddHandler m_values.ItemCheck, AddressOf onValueChecked

        m_summary = New Label With {
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(148, 163, 184),
            .AutoEllipsis = True,
            .Text = ""
        }

        Call panel.Controls.Add(m_valueHint, 0, 0)
        Call panel.Controls.Add(buttons, 0, 1)
        Call panel.Controls.Add(m_values, 0, 2)
        Call panel.Controls.Add(m_summary, 0, 3)

        Return panel
    End Function

    ''' <summary>右侧：GPU 画布 + 状态行。</summary>
    Private Function createCanvas() As Control
        Dim panel As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Padding = New Padding(0, 6, 8, 8),
            .BackColor = Me.BackColor
        }

        Call panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Call panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 26))

        m_canvas = New DxCanvas With {
            .Dock = DockStyle.Fill,
            .AutoClear = True,
            .BackgroundColor = m_theme.BackgroundColor
        }
        AddHandler m_canvas.Render, AddressOf onRender

        m_status = New Label With {
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(148, 163, 184),
            .AutoEllipsis = True,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Text = ""
        }

        Call panel.Controls.Add(m_canvas, 0, 0)
        Call panel.Controls.Add(m_status, 0, 1)

        Return panel
    End Function

#End Region

#Region "数据装配"

    ''' <summary>
    ''' 按给定参数切换视图（供自动化自检与外部调用；界面上等价于改三个下拉框）。
    ''' </summary>
    Public Sub ApplyOptions(dimension As NeuronLabelDimension,
                            mode As ResponseSignalMode,
                            aggregation As CurveAggregation)

        m_loading = True

        Try
            m_dimensionBox.SelectedIndex = dimensionIndex(dimension)
            m_aggregationBox.SelectedIndex = aggregationIndex(aggregation)

            ' 口径可能不在可用列表里（例如这份记录没有膜电位），此时退到第一个可用口径
            Dim modes As ResponseSignalMode() = m_data.AvailableModes
            Dim index As Integer = System.Array.IndexOf(modes, mode)

            m_modeBox.SelectedIndex = If(index >= 0, index, 0)
        Finally
            m_loading = False
        End Try

        Call refreshCategories()
        Call rebuildSeries()
    End Sub

    Private Shared Function dimensionIndex(dimension As NeuronLabelDimension) As Integer
        Select Case dimension
            Case NeuronLabelDimension.Neurotransmitter
                Return 1
            Case NeuronLabelDimension.PrimaryType
                Return 2
            Case NeuronLabelDimension.SuperClass
                Return 3
            Case NeuronLabelDimension.CellClass
                Return 4
            Case NeuronLabelDimension.CellGroup
                Return 5
            Case Else
                Return 0
        End Select
    End Function

    Private Shared Function aggregationIndex(aggregation As CurveAggregation) As Integer
        Select Case aggregation
            Case CurveAggregation.GroupMean
                Return 1
            Case CurveAggregation.GroupEnvelope
                Return 2
            Case Else
                Return 0
        End Select
    End Function

    Private Function currentDimension() As NeuronLabelDimension
        Select Case m_dimensionBox.SelectedIndex
            Case 1
                Return NeuronLabelDimension.Neurotransmitter
            Case 2
                Return NeuronLabelDimension.PrimaryType
            Case 3
                Return NeuronLabelDimension.SuperClass
            Case 4
                Return NeuronLabelDimension.CellClass
            Case 5
                Return NeuronLabelDimension.CellGroup
            Case Else
                Return NeuronLabelDimension.Neuropil
        End Select
    End Function

    Private Function currentMode() As ResponseSignalMode
        Dim modes As ResponseSignalMode() = m_data.AvailableModes
        Dim index As Integer = System.Math.Max(0, System.Math.Min(m_modeBox.SelectedIndex, modes.Length - 1))

        Return modes(index)
    End Function

    Private Function currentAggregation() As CurveAggregation
        Select Case m_aggregationBox.SelectedIndex
            Case 1
                Return CurveAggregation.GroupMean
            Case 2
                Return CurveAggregation.GroupEnvelope
            Case Else
                Return CurveAggregation.Individual
        End Select
    End Function

    Private Function selectedValues() As String()
        Dim picked As New List(Of String)()

        For i As Integer = 0 To m_values.CheckedItems.Count - 1
            Call picked.Add(unwrap(m_values.CheckedItems(i).ToString))
        Next

        Return picked.ToArray()
    End Function

    ''' <summary>取值列表项被勾选 / 取消勾选。</summary>
    Private Sub onValueChecked(sender As Object, e As ItemCheckEventArgs)
        ' ItemCheck 触发时勾选状态还没提交，因此用 BeginInvoke 等到状态更新完再重画
        Call BeginInvoke(New Action(AddressOf rebuildSeries))
    End Sub

    Private Sub onDimensionChanged(sender As Object, e As EventArgs)
        If m_loading Then Return

        Call refreshCategories()
        Call rebuildSeries()
    End Sub

    Private Sub onOptionChanged(sender As Object, e As EventArgs)
        If m_loading Then Return

        Call rebuildSeries()
    End Sub

    ''' <summary>刷新标签取值列表（只列出当前响应集合里出现过的取值）。</summary>
    Private Sub refreshCategories()
        m_loading = True

        Try
            Dim counts As Integer() = Nothing
            Dim values As String() = m_data.Categories(currentDimension(), m_dataset, counts)

            Call m_values.Items.Clear()

            For i As Integer = 0 To values.Length - 1
                Call m_values.Items.Add($"{values(i)}  ({counts(i):N0})")
            Next

            m_valueHint.Text = $"{ResponseCurveBuilder.DescribeDimension(currentDimension())}：" &
                               $"{values.Length} 个取值 / {m_data.Responders.Length:N0} 个响应神经元"
            Call updateSummary()
        Finally
            m_loading = False
        End Try
    End Sub

    Private Sub setAllValues(checked As Boolean)
        m_loading = True

        Try
            For i As Integer = 0 To m_values.Items.Count - 1
                m_values.SetItemChecked(i, checked)
            Next
        Finally
            m_loading = False
        End Try

        Call rebuildSeries()
    End Sub

    ''' <summary>把列表项文本（"名称  (n)"）还原成纯标签值。</summary>
    Private Shared Function unwrap(item As String) As String
        If item Is Nothing Then Return ""

        Dim index As Integer = item.LastIndexOf("  (")

        If index > 0 Then Return item.Substring(0, index)

        Return item
    End Function

    ''' <summary>重新装配曲线集合并请求重绘。</summary>
    Private Sub rebuildSeries()
        Dim options As New CurveOptions With {
            .Dimension = currentDimension(),
            .Selected = selectedValues(),
            .Mode = currentMode(),
            .Aggregation = currentAggregation(),
            .MaxCurves = CInt(m_limitBox.Value),
            .Window = CInt(m_windowBox.Value)
        }

        m_series = ResponseCurveBuilder.Build(m_data, m_dataset, options, m_description)
        Me.Text = $"电刺激响应曲线 — 神经元 #{m_data.TargetNeuron} (root_id {m_data.TargetRootId})"

        Call updateSummary()
        Call m_canvas.Invalidate()
    End Sub

    Private Sub updateSummary()
        Dim modes As ResponseSignalMode() = m_data.AvailableModes
        Dim potentialNote As String = ""

        If m_data.PotentialValidated.HasValue Then
            potentialNote = If(m_data.PotentialValidated.Value,
                               "；膜电位已与快速仿真逐位校验一致",
                               $"；⚠ 膜电位与快速仿真有 {m_data.PotentialMismatches} 处不一致，仅供参考")
        Else
            potentialNote = "；本次记录没有膜电位（曲线由脉冲折算）"
        End If

        m_summary.Text = $"{m_description}{Environment.NewLine}" &
                         $"响应神经元 {m_data.Responders.Length:N0} / 全脑 {m_data.Units:N0}，T={m_data.Steps} 步{potentialNote}"
    End Sub

#End Region

#Region "绘制"

    ''' <summary>
    ''' 把曲线画到控件的 GPU 画布上。
    ''' </summary>
    ''' <remarks>
    ''' 每帧新建一个 <c>LinePlot</c>：绘图引擎的排版尺寸取自画布尺寸，
    ''' 控件一旦缩放就需要按新尺寸重新排版，复用旧对象会画到旧尺寸上。
    ''' 因为画布是外部注入的，<c>Dispose</c> 不会关闭控件自己的绘图设备。
    ''' </remarks>
    Private Sub onRender(sender As Object, e As DxRenderEventArgs)
        If e.Graphics Is Nothing Then Return

        Using plot As New LinePlot(e.Graphics, m_theme)
            plot.Title = $"electric stimulation response — neuron #{m_data.TargetNeuron}"
            plot.SubTitle = $"纵轴 = {ResponseCurveBuilder.DescribeMode(currentMode())}" &
                            If(currentMode() = ResponseSignalMode.MembranePotential, " (阈值 " & m_data.Threshold.ToString("G4") & ") ", " ") &
                            $"· 横轴 = 时间步 · " &
                            ResponseCurveBuilder.Describe(New CurveOptions With {
                                .Dimension = currentDimension(),
                                .Selected = selectedValues()
                            })
            plot.XLabel = "time step"

            ' 纵轴量纲写在副标题里而不是用 YLabel：绘图库的 Y 轴标题排版在 GPU 画布上
            ' 会被放到绘图区内部（旋转后的定位不跟随画布变换），副标题更稳也更清楚
            plot.ShowLegend = m_series IsNot Nothing AndAlso m_series.Count > 1 AndAlso m_series.Count <= 24
            plot.LegendLocation = PlotEngine.LegendPos.UpperRight

            If m_series IsNot Nothing AndAlso m_series.Count > 0 Then
                Call plot.Plot(m_series)
            Else
                Call plot.Plot(New List(Of Series)() From {
                    New Series With {
                        .Name = "(没有可绘制的曲线)",
                        .X = New Double() {1, 2},
                        .Y = New Double() {0, 0},
                        .Visible = False
                    }
                })
            End If
        End Using

        m_status.Text = $"{m_description} · 画布 {e.Width}x{e.Height} px · {m_data.Source}"
        m_status.ForeColor = Color.FromArgb(148, 163, 184)
    End Sub

    Private Sub onSaveImage(sender As Object, e As EventArgs)
        Using dialog As New SaveFileDialog() With {
            .Filter = "PNG 图片|*.png",
            .FileName = $"stimulation_response_{m_data.TargetNeuron}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        }

            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return

            If CaptureTo(dialog.FileName) Then
                m_status.Text = $"已保存: {dialog.FileName}"
            Else
                m_status.Text = $"保存失败: {m_canvas.LastError}"
            End If
        End Using
    End Sub

    ''' <summary>把控件当前画面（也就是曲线图）抓成一帧写盘。</summary>
    ''' <remarks>
    ''' 走的是控件的 GPU 画布回读：强制同步重绘一帧并保存，
    ''' 因此拿到的是屏幕上真正显示的内容，而不是另画一遍。
    ''' </remarks>
    Public Function CaptureTo(file As String) As Boolean
        Return m_canvas.SaveImage(file)
    End Function

    Private Sub InitializeComponent()

    End Sub

    ''' <summary>GPU 画布是否已经就绪（供自动化自检等待首帧）。</summary>
    Public ReadOnly Property CanvasReady As Boolean
        Get
            Return m_canvas IsNot Nothing AndAlso m_canvas.IsCanvasCreated
        End Get
    End Property

#End Region

#Region "离屏出图（命令行 / 自动化验证）"

    ''' <summary>
    ''' 把响应曲线渲染成一张图片文件（不需要窗口，用于命令行出图与自动化验证）。
    ''' </summary>
    ''' <param name="file">输出 png 路径</param>
    ''' <param name="data">响应数据</param>
    ''' <param name="dataset">数据集</param>
    ''' <param name="options">曲线参数</param>
    ''' <param name="width">画布宽 (像素)</param>
    ''' <param name="height">画布高 (像素)</param>
    ''' <remarks>
    ''' 走的是与窗口<b>完全相同</b>的代码路径（同一个 <c>LinePlot</c> + 同一套曲线装配），
    ''' 区别只是画布换成离屏 <c>DxGraphics</c> —— 因此它既能导出图片，
    ''' 也能在没有窗口的环境里验证绘图链路。
    ''' </remarks>
    Public Shared Function RenderToFile(file As String,
                                        data As ResponseDataset,
                                        dataset As BrainDataset,
                                        options As CurveOptions,
                                        Optional width As Integer = 1600,
                                        Optional height As Integer = 900,
                                        Optional ByRef description As String = Nothing) As Boolean

        Call PlotRuntime.EnsureRegistered()

        Dim theme As PlotTheme = PlotTheme.Dark()
        Dim series As List(Of Series) = ResponseCurveBuilder.Build(data, dataset, options, description)

        Using g As New DxGraphics(width, height, theme.BackgroundColor)
            Using plot As New LinePlot(g, theme)
                plot.Title = $"electric stimulation response — neuron #{data.TargetNeuron}"
                plot.SubTitle = $"纵轴 = {ResponseCurveBuilder.DescribeMode(options.Mode)} · 横轴 = 时间步 · " &
                                $"{ResponseCurveBuilder.Describe(options)}"
                plot.XLabel = "time step"
                plot.ShowLegend = series.Count > 1 AndAlso series.Count <= 24
                plot.LegendLocation = PlotEngine.LegendPos.UpperRight

                Call plot.Plot(If(series.Count > 0, series, New List(Of Series)() From {
                    New Series With {.Name = "(没有可绘制的曲线)", .X = New Double() {1, 2}, .Y = New Double() {0, 0}, .Visible = False}
                }))
            End Using

            Return g.Save(file)
        End Using
    End Function

#End Region

End Class
