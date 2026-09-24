Namespace AppLogics

    ''' <summary>
    ''' 出图模式下的电刺激规格（离线验证回放渲染）。
    ''' </summary>
    Public Structure SnapshotStimulus
        Public Neuron As Integer
        Public RadiusNm As Double
        Public Current As Double
        Public StepIndex As Integer
    End Structure
End Namespace