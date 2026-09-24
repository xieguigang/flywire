Imports Galaxy.Workbench
Imports Galaxy.Workbench.CommonDialogs
Imports Microsoft.VisualStudio.WinForms.Docking
Imports Neuropils.RibbonLib.Controls
Imports RibbonLib

Module RibbonMenu

    Public ReadOnly Property ribbon As RibbonItems

    ''' <summary>
    ''' 是否进入针对大脑神经元的电刺激实验模式
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property ToggleSimulationExperiment As Boolean
        Get
            Return ribbon.ToggleSimulationExperiment.BooleanValue
        End Get
    End Property

    Public ReadOnly Property SimulationSteps As Integer
        Get
            Return CInt(_ribbon.NumSimulationSteps.DecimalValue)
        End Get
    End Property

    Public ReadOnly Property SimulationIntensity As Double
        Get
            Return CDbl(_ribbon.NumIntensity.DecimalValue)
        End Get
    End Property

    Public Sub Load(ribbon As Ribbon)
        _ribbon = New RibbonItems(ribbon)

        AddHandler RibbonMenu.ribbon.ButtonOpenSnakeGame.ExecuteEvent, Sub() Call Workbench.m_snake.openSnakeWindow()
        AddHandler RibbonMenu.ribbon.ButtonOpenResponseCharts.ExecuteEvent, AddressOf onOpenResponseChart
        AddHandler RibbonMenu.ribbon.ButtonAbout.ExecuteEvent, Sub() Call InputDialog.ShowDialog(Of AboutForm)()
        AddHandler RibbonMenu.ribbon.ButtonResetView.ExecuteEvent, Sub() Call Workbench.flywire.m_canvas.ResetView()
        AddHandler RibbonMenu.ribbon.ButtonOpenModelDir.ExecuteEvent, Sub() Workbench.flywire.onReload()
        AddHandler RibbonMenu.ribbon.ButtonMakeScreenshot.ExecuteEvent, Sub() Workbench.flywire.onSnapshot()
        AddHandler RibbonMenu.ribbon.ButtonOpenCanvas.ExecuteEvent, Sub() Workbench.flywire.DockState = DockState.Document

        _ribbon.NumIntensity.MinValue = 0.2
        _ribbon.NumIntensity.MaxValue = 20
        _ribbon.NumIntensity.Increment = 0.5
        _ribbon.NumIntensity.DecimalValue = 1

        _ribbon.NumSimulationSteps.MinValue = 5
        _ribbon.NumSimulationSteps.MaxValue = 250
        _ribbon.NumSimulationSteps.DecimalValue = 30

        _ribbon.NumSynapseCutoff.MinValue = 1
        _ribbon.NumSynapseCutoff.MaxValue = 100000
        _ribbon.NumSynapseCutoff.Increment = 1
        _ribbon.NumSynapseCutoff.DecimalValue = m_buildOptions.SynapseThreshold

        _ribbon.NumScatterSize.MinValue = 1
        _ribbon.NumScatterSize.MaxValue = 12
        _ribbon.NumScatterSize.Increment = 1
        _ribbon.NumScatterSize.DecimalValue = 2

        AddHandler RibbonMenu.ribbon.NumSimulationSteps.ExecuteEvent, AddressOf onStimulusStepsChanged
        AddHandler RibbonMenu.ribbon.NumSynapseCutoff.ExecuteEvent, AddressOf onThresholdChanged
        AddHandler RibbonMenu.ribbon.NumScatterSize.ExecuteEvent, AddressOf onPointSizeChanged
    End Sub

    Private Sub onThresholdChanged(sender As Object, e As EventArgs)
        m_buildOptions.SynapseThreshold = CInt(RibbonMenu.ribbon.NumSynapseCutoff.DecimalValue)

        ' 阈值只影响连线，点云不动
        Call Workbench.flywire.scheduleRebuild()
    End Sub

    Private Sub onPointSizeChanged(sender As Object, e As EventArgs)
        Workbench.flywire.m_canvas.PointSize = CInt(RibbonMenu.ribbon.NumScatterSize.DecimalValue)
    End Sub

    ''' <summary>工具条"仿真步数"：转交给 StimulationExperiment 处理步数变更。</summary>
    Private Sub onStimulusStepsChanged(sender As Object, e As EventArgs)
        If m_experiment IsNot Nothing Then
            Call m_experiment.onStimulusStepsChanged(sender, e)
        End If
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

        Static m_chartForm As PageResponseChart = Nothing

        If m_chartForm IsNot Nothing AndAlso Not m_chartForm.IsDisposed Then
            Call m_chartForm.Close()
        End If

        m_chartForm = New PageResponseChart(data, m_dataset)
        m_chartForm.Show(CommonRuntime.AppHost.GetDockPanel)
        m_chartForm.DockState = DockState.Document
    End Sub
End Module
