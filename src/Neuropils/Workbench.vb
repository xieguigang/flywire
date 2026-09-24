Imports System.Text
Imports Neuropils.Data

Module Workbench

    ''' <summary>
    ''' 数据目录的默认位置 (与 SNN 仿真使用的是同一份 FAFB v783 数据)。
    ''' </summary>
    Public Const DefaultDataDir As String = "F:\flywire\FAFB-v783"

    Friend m_dataset As BrainDataset
    Friend flywire As PageFlywireCanvas
    Friend m_config As New VisualizationConfig()

    Public Sub check(report As StringBuilder, ByRef failures As Integer, label As String, actual As Object, expected As Object)
        Dim ok As Boolean = String.Equals(Convert.ToString(actual), Convert.ToString(expected))

        If Not ok Then
            failures += 1
        Else
            Call report.AppendLine($"      {(If(ok, "[OK]  ", "[FAIL]"))} {label}: actual={actual}, expected={expected}")
        End If
    End Sub
End Module
