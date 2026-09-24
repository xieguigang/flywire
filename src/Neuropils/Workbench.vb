Imports System.Runtime.CompilerServices
Imports System.Text
Imports Neuropils.AppLogics
Imports Neuropils.Data

Module Workbench

    ''' <summary>
    ''' 数据目录的默认位置 (与 SNN 仿真使用的是同一份 FAFB v783 数据)。
    ''' </summary>
    Public Const DefaultDataDir As String = "F:\flywire\FAFB-v783"

    Friend m_dataset As BrainDataset
    Friend flywire As PageFlywireCanvas
    Friend m_config As New VisualizationConfig()
    Friend m_experiment As StimulationExperiment
    Friend m_snake As Snake

    Public ReadOnly Property progress As ToolStripProgressBar

    Dim _main As FormMain

    Public Sub Load(main As FormMain)
        _main = main
        _progress = main.m_progress
    End Sub

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Sub SceneText(msg As String)
        _main.m_sceneText.Text = msg
    End Sub

    Public Sub check(report As StringBuilder, ByRef failures As Integer, label As String, actual As Object, expected As Object)
        Dim ok As Boolean = String.Equals(Convert.ToString(actual), Convert.ToString(expected))

        If Not ok Then
            failures += 1
        Else
            Call report.AppendLine($"      {(If(ok, "[OK]  ", "[FAIL]"))} {label}: actual={actual}, expected={expected}")
        End If
    End Sub
End Module
