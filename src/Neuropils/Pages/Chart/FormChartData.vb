Imports System.ComponentModel
Imports Galaxy.Workbench
Imports Neuropils.Analysis

Public Class FormChartData

    Friend m_loading As Boolean

    Dim m_host As PageResponseChart

    ''' <summary>
    ''' 填充"响应信号"下拉框：只列出当前数据真正可用的口径
    ''' (没有分析回放时就不给出"膜电位"这个选项)。
    ''' </summary>
    ''' <remarks>
    ''' 下拉框本身在 Designer 的 <c>InitializeComponent</c> 里建好，
    ''' 但可选项由数据决定，因此放到这里；<c>m_loading</c> 用来抑制
    ''' 初始选中项触发的事件 —— 那时其余控件刚建好，还不该重建曲线。
    ''' </remarks>
    Public Sub bindSignalModes(host As PageResponseChart)
        m_loading = True
        m_host = host

        Try
            For Each mode As ResponseSignalMode In m_host.m_data.AvailableModes
                Call m_modeBox.Items.Add(ResponseCurveBuilder.DescribeMode(mode))
            Next

            m_modeBox.SelectedIndex = 0
        Finally
            m_loading = False
        End Try
    End Sub

    ''' <summary>取值列表项被勾选 / 取消勾选。</summary>
    Private Sub onValueChecked(sender As Object, e As ItemCheckEventArgs) Handles m_values.ItemCheck
        ' ItemCheck 触发时勾选状态还没提交，因此用 BeginInvoke 等到状态更新完再重画
        BeginInvoke(New Action(AddressOf m_host.rebuildSeries))
    End Sub

    Private Sub onDimensionChanged(sender As Object, e As EventArgs) Handles m_dimensionBox.SelectedIndexChanged
        If m_loading Then Return

        refreshCategories()
        m_host.rebuildSeries()
    End Sub

    Private Sub onOptionChanged(sender As Object, e As EventArgs) Handles m_modeBox.SelectedIndexChanged,
        m_aggregationBox.SelectedIndexChanged,
        m_limitBox.ValueChanged,
        m_windowBox.ValueChanged

        If m_loading OrElse m_host Is Nothing Then
            Return
        End If

        m_host.rebuildSeries()
    End Sub

    ''' <summary>刷新标签取值列表（只列出当前响应集合里出现过的取值）。</summary>
    Friend Sub refreshCategories()
        m_loading = True

        Try
            Dim counts As Integer() = Nothing
            Dim values As String() = m_host.m_data.Categories(m_host.currentDimension(), m_dataset, counts)

            Call m_values.Items.Clear()

            For i As Integer = 0 To values.Length - 1
                Call m_values.Items.Add($"{values(i)}  ({counts(i):N0})")
            Next

            m_valueHint.Text = $"{ResponseCurveBuilder.DescribeDimension(m_host.currentDimension())}：" &
                               $"{values.Length} 个取值 / {m_host.m_data.Responders.Length:N0} 个响应神经元"
            Call m_host.updateSummary()
        Finally
            m_loading = False
        End Try
    End Sub

    Private Sub selectAllEvt() Handles selectAll.Click
        setAllValues(True)
    End Sub

    Private Sub clearAllEvt() Handles clearAll.Click
        setAllValues(False)
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

        Call m_host.rebuildSeries()
    End Sub

    Private Sub onSaveImage(sender As Object, e As EventArgs) Handles exportImage.Click
        Using dialog As New SaveFileDialog With {
            .Filter = "PNG 图片|*.png",
            .FileName = $"stimulation_response_{m_host.m_data.TargetNeuron}_{Date.Now:yyyyMMdd_HHmmss}.png"
        }

            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return

            If m_host.CaptureTo(dialog.FileName) Then
                CommonRuntime.StatusMessage($"已保存: {dialog.FileName}")
            Else
                CommonRuntime.Warning($"保存失败: {m_host.m_canvas.LastError}")
            End If
        End Using
    End Sub

    Friend optFormClosed As Boolean

    Private Sub FormChartData_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        optFormClosed = True
    End Sub
End Class