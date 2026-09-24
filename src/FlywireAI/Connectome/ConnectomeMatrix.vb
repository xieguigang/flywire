Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork

Namespace Connectome.Network

    ''' <summary>
    ''' 连接组的 CSR 权重矩阵 (结构归一化之后的单位权重)，可以在多种刺激方案之间复用。
    ''' </summary>
    Public Class ConnectomeMatrix

        Public ReadOnly Property Synapses As SparseMatrix
        Public ReadOnly Property BaseValues As Double()
        Public ReadOnly Property StructuralFanIn As Double()
        Public ReadOnly Property Triplets As SynapseTriplets

        ''' <summary>当前已经写入 CSR 的全局增益。</summary>
        ''' <remarks>
        ''' <see cref="BuildMatrix"/> 完成后 CSR 里就是单位权重 (gain = 1)。
        ''' </remarks>
        Public Property CurrentGain As Double = 1.0

        Friend Sub New(synapses As SparseMatrix, baseValues As Double(), fanIn As Double(), triplets As SynapseTriplets)
            Me.Synapses = synapses
            Me.BaseValues = baseValues
            Me.StructuralFanIn = fanIn
            Me.Triplets = triplets
        End Sub

        ''' <summary>
        ''' 就地应用全局增益 (幂等：增益没变时什么都不做)。
        ''' </summary>
        ''' <returns>是否真的重写了权重。</returns>
        ''' <remarks>
        ''' <b>幂等性是性能要求而不是优化技巧</b>：重写权重会调用
        ''' <c>SparseMatrix.MarkModified()</c>，使设备端 (GPU) 缓存的 CSR 副本失效 ——
        ''' 下一次稀疏乘法就得把整张连接表重新上传 (nnz = 373 万时约 60 MB，
        ''' 在 WDDM 上约 10 ms)。而"标定完成后用同一个增益正式仿真"是常规流程，
        ''' 不幂等会让这 10 ms 落进每一次仿真的计时窗口里，
        ''' 足以把 GPU 的加速比从 5x 以上拉低到 3x 附近。
        ''' </remarks>
        Public Function ApplyGain(gain As Double) As Boolean
            If gain = CurrentGain Then Return False

            Call SynapseTriplets.ApplyGain(Synapses, BaseValues, gain)

            CurrentGain = gain

            Return True
        End Function

        ''' <summary>神经元总数 N。</summary>
        Public ReadOnly Property Units As Integer
            Get
                Return Synapses.Columns
            End Get
        End Property

        ''' <summary>非零突触数量。</summary>
        Public ReadOnly Property Nnz As Integer
            Get
                Return Synapses.NonZeros
            End Get
        End Property

        Public Overrides Function ToString() As String
            Return $"{Units} neurons, nnz={Nnz} (csv rows={Triplets.CsvRows})"
        End Function

    End Class

End Namespace