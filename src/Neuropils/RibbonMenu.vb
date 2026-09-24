Imports Galaxy.Workbench
Imports Microsoft.VisualStudio.WinForms.Docking
Imports Neuropils.RibbonLib.Controls
Imports RibbonLib

Module RibbonMenu

    Public ReadOnly Property ribbon As RibbonItems

    Public ReadOnly Property ToggleSimulationExperiment As Boolean
        Get
            Return ribbon.ToggleSimulationExperiment.BooleanValue
        End Get
    End Property

    Public Sub Load(ribbon As Ribbon)
        _ribbon = New RibbonItems(ribbon)

        AddHandler RibbonMenu.ribbon.ButtonOpenSnakeGame.ExecuteEvent, Sub() Call Workbench.m_snake.openSnakeWindow()
        AddHandler RibbonMenu.ribbon.ButtonOpenResponseCharts.ExecuteEvent, AddressOf onOpenResponseChart
    End Sub

    ''' <summary>
    ''' 打开响应曲线窗口。
    ''' </summary>
    ''' <remarks>
    ''' 数据优先级：<b>本次会话刚跑完的刺激结果</b>（含膜电位分析回放）→
    ''' 否则读<b>最近一次落盘的实验记录目录</b>。因此即使重开程序，
    ''' "记录下来的响应结果"也还能画出来。
    ''' </remarks>
    Public Sub onOpenResponseChart()
        If m_dataset Is Nothing Then
            Return
        End If

        '  Try
        Dim data As Analysis.ResponseDataset

        If m_experiment.m_replay IsNot Nothing Then
            data = Analysis.ResponseDataset.FromReplay(m_experiment.m_replay, m_dataset, m_config.Threshold)
        Else
            Dim dir As String = Analysis.StimulationArchive.Latest(m_config.ResolveActivityDir())

            If dir Is Nothing Then
                Call MessageBox.Show(Workbench.flywire,
                                     "还没有任何电刺激实验记录。请先勾选「电刺激模式」，" &
                                     "在神经元上按住左键做一次刺激，松开后即可查看响应曲线。",
                                     "没有实验记录", MessageBoxButtons.OK, MessageBoxIcon.Information)

                Return
            End If

            data = Analysis.ResponseDataset.FromReport(dir, m_dataset)
        End If

        Static m_chartForm As ResponseChartForm = Nothing

        If m_chartForm IsNot Nothing AndAlso Not m_chartForm.IsDisposed Then
            Call m_chartForm.Close()
        End If

        m_chartForm = New ResponseChartForm(data, m_dataset)
        m_chartForm.Show(CommonRuntime.AppHost.GetDockPanel)
        m_chartForm.DockState = DockState.Document
    End Sub
End Module
