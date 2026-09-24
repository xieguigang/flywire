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
            For Each mode As ResponseSignalMode In m_host . m_data.AvailableModes
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
        BeginInvoke(New Action(AddressOf rebuildSeries))
    End Sub

    Private Sub onDimensionChanged(sender As Object, e As EventArgs) Handles m_dimensionBox.SelectedIndexChanged
        If m_loading Then Return

        refreshCategories()
        rebuildSeries()
    End Sub

    Private Sub onOptionChanged(sender As Object, e As EventArgs) Handles m_modeBox.SelectedIndexChanged,
        m_aggregationBox.SelectedIndexChanged,
        m_limitBox.ValueChanged,
        m_windowBox.ValueChanged

        If m_opt.m_loading Then
            Return
        End If

        rebuildSeries()
    End Sub

    Private Sub selectAllEvt() Handles selectAll.Click
        setAllValues(True)
    End Sub

    Private Sub clearAllEvt() Handles clearAll.Click
        setAllValues(False)
    End Sub

    Private Sub onSaveImage(sender As Object, e As EventArgs) Handles exportImage.Click
        Using dialog As New SaveFileDialog With {
            .Filter = "PNG 图片|*.png",
            .FileName = $"stimulation_response_{m_data.TargetNeuron}_{Date.Now:yyyyMMdd_HHmmss}.png"
        }

            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return

            If m_host.CaptureTo(dialog.FileName) Then
                StatusMessage($"已保存: {dialog.FileName}")
            Else
                warning($"保存失败: {m_canvas.LastError}")
            End If
        End Using
    End Sub
End Class