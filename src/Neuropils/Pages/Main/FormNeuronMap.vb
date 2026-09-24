Imports Microsoft.VisualBasic.Drawing
Imports Microsoft.VisualBasic.Imaging
Imports Microsoft.VisualBasic.Imaging.Drawing2D.Colors
Imports Microsoft.VisualStudio.WinForms.Docking
Imports Neuropils.Data

Public Class FormNeuronMap

#Region "legend"

    ''' <summary>刷新图例列表 (热力图维度下显示色标而不是勾选列表)。</summary>
    Friend Sub refreshLegend()
        Call m_legend.Items.Clear()

        If m_colorizer Is Nothing Then Return

        If m_colorizer.IsHeatMap Then
            m_legend.Enabled = False
            Call m_legend.Items.Add($"活跃度热力图 (最小 {minActivity():F0} / 最大 {maxActivity():F0} 脉冲)")

            Return
        End If

        m_legend.Enabled = True

        For Each item As ColorLegendItem In m_colorizer.Legend
            Dim index As Integer = m_legend.Items.Add(item)

            m_legend.SetItemChecked(index, item.Visible)
        Next
    End Sub

    Private Sub onLegendItemCheck(sender As Object, e As ItemCheckEventArgs) Handles m_legend.ItemCheck
        If m_colorizer Is Nothing OrElse m_colorizer.IsHeatMap Then Return
        If e.Index < 0 OrElse e.Index >= m_legend.Items.Count Then Return

        Dim item = TryCast(m_legend.Items(e.Index), ColorLegendItem)

        If item Is Nothing Then Return

        m_colorizer.SetVisible(item, e.NewValue = CheckState.Checked)

        ' 勾选状态在事件返回之后才生效，因此重建延后到消息循环的空闲时刻
        BeginInvoke(New Action(AddressOf Workbench.flywire.scheduleRebuild))
    End Sub

    Private Function minActivity() As Double
        Return activityRange().Item1
    End Function

    Private Function maxActivity() As Double
        Return activityRange().Item2
    End Function

    Private Function activityRange() As (Double, Double)
        If m_dataset Is Nothing OrElse Not m_dataset.HasActivity Then Return (0.0, 0.0)

        Dim min As Double = Double.MaxValue
        Dim max As Double = Double.MinValue

        For Each value As Double In m_dataset.Activity
            If value < min Then min = value
            If value > max Then max = value
        Next

        Return (min, max)
    End Function

    ''' <summary>画出当前配色方案的色标 (热力图维度)。</summary>
    Public Sub showGradient()
        Dim colors As Color() = Designer.GetColors("viridis", 256, 255)
        Dim bitmap As New Bitmap(256, 1)

        For i As Integer = 0 To 255
            bitmap.SetPixel(i, 0, colors(i))
        Next

        ' 旧的位图要主动释放：切换维度会反复生成色标
        Dim stale As System.Drawing.Image = m_gradient.Image

        m_gradient.Image = bitmap.CTypeGdiImage
        m_gradient.Visible = True

        If stale IsNot Nothing Then
            stale.Dispose()
        End If
    End Sub

    Private Sub FormNeuronMap_Load(sender As Object, e As EventArgs) Handles Me.Load
        Width = 120

        Call refreshLegend()
    End Sub

#End Region


    ''' <summary>
    ''' 回放控制面板各按钮 / 滑块的事件包装：控件已在 InitializeComponent 中声明式建好，
    ''' 这里仅以 Handles 绑定后转交给 StimulationExperiment 处理。
    ''' </summary>
    Private Sub onReplayFirst(sender As Object, e As EventArgs) Handles m_replayFirst.Click
        m_experiment.onReplayFirst(sender, e)
    End Sub

    Private Sub onReplayPrev(sender As Object, e As EventArgs) Handles m_replayPrev.Click
        m_experiment.onReplayPrev(sender, e)
    End Sub

    Private Sub onReplayPlay(sender As Object, e As EventArgs) Handles m_replayPlay.Click
        m_experiment.onReplayPlay(sender, e)
    End Sub

    Private Sub onReplayNext(sender As Object, e As EventArgs) Handles m_replayNext.Click
        m_experiment.onReplayNext(sender, e)
    End Sub

    Private Sub onReplayLast(sender As Object, e As EventArgs) Handles m_replayLast.Click
        m_experiment.onReplayLast(sender, e)
    End Sub

    Private Sub onReplayClear(sender As Object, e As EventArgs) Handles m_replayClear.Click
        m_experiment.onReplayClear(sender, e)
    End Sub

    Private Sub onReplayTrackScroll(sender As Object, e As EventArgs) Handles m_replayTrack.Scroll
        m_experiment.onReplayTrackScroll(sender, e)
    End Sub

    Private Sub onReplaySpeedChanged(sender As Object, e As EventArgs) Handles m_replaySpeed.Click
        If m_experiment IsNot Nothing Then
            m_experiment.onReplaySpeedChanged(sender, e)
        End If
    End Sub

    Private Sub FormNeuronMap_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        e.Cancel = True
        DockState = DockState.Hidden
    End Sub
End Class