Imports FlywireAI.Connectome.Network
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork

Namespace Connectome

    ''' <summary>
    ''' 由突触三元组装配出来的果蝇全脑脉冲神经网络 (单个稀疏递归 LIF 层)。
    ''' 
    ''' 网络拓扑来自 ``connections_princeton.csv`` (139,255 个神经元 / 534 万条突触)，
    ''' 权重由 <see cref="SynapseTriplets"/> 按 ``nt_type`` 的极性生成，并按照突触后神经元的
    ''' ``Σ|w|`` 做结构归一化；<see cref="SetGain"/> 可以在**不重建 CSR** 的前提下缩放全局增益
    ''' (CPU 张量后端会就地读取 <c>SparseMatrix.Values</c>)。
    ''' </summary>
    Public Class BrainNetwork

        ''' <summary>SNN 库的稀疏递归网络对象。</summary>
        Public ReadOnly Property Network As SpikingNetwork

        ''' <summary>CSR 稀疏权重矩阵 (行 = 突触前，列 = 突触后)。</summary>
        Public ReadOnly Property Synapses As SparseMatrix

        ''' <summary>神经元总数 N。</summary>
        Public ReadOnly Property Units As Integer

        ''' <summary>构建本网络所使用的突触三元组。</summary>
        Public ReadOnly Property Triplets As SynapseTriplets

        ''' <summary>结构归一化之后的单位权重 (即 gain = 1 时的权重)。</summary>
        Public ReadOnly Property BaseValues As Double()

        ''' <summary>每个突触后神经元在归一化之前的 ``Σ|w|``。</summary>
        Public ReadOnly Property StructuralFanIn As Double()

        ''' <summary>当前生效的全局权重增益。</summary>
        Public Property Gain As Double
            Get
                Return _gain
            End Get
            Private Set(value As Double)
                _gain = value
            End Set
        End Property

        Private _gain As Double

        ''' <summary>
        ''' 本网络所基于的连接组矩阵。
        ''' </summary>
        ''' <remarks>
        ''' 增益状态由它持有（<see cref="ConnectomeMatrix.CurrentGain"/>）：
        ''' 多个网络共享同一个 CSR 时，"权重里现在写的是哪个增益"只能有一个答案，
        ''' 否则 <see cref="SetGain"/> 的幂等判断会与实际权重脱节。
        ''' </remarks>
        Public ReadOnly Property Matrix As ConnectomeMatrix

        Friend Sub New(network As SpikingNetwork,
                       matrix As ConnectomeMatrix,
                       synapses As SparseMatrix,
                       triplets As SynapseTriplets,
                       baseValues As Double(),
                       structuralFanIn As Double(),
                       gain As Double)

            Me.Network = network
            Me.Matrix = matrix
            Me.Synapses = synapses
            Me.Triplets = triplets
            Me.BaseValues = baseValues
            Me.StructuralFanIn = structuralFanIn
            Me.Units = synapses.Columns
            Me._gain = gain
        End Sub

        ''' <summary>非零突触数量 (即合并重复 (pre, post) 之后的边数)。</summary>
        Public ReadOnly Property Nnz As Integer
            Get
                Return Synapses.NonZeros
            End Get
        End Property

        ''' <summary>
        ''' 就地设置全局权重增益 (不重建 CSR，因此标定过程非常廉价)。
        ''' </summary>
        ''' <remarks>
        ''' 幂等：增益未变时不重写权重。原因见 <see cref="ConnectomeMatrix.ApplyGain"/> ——
        ''' 无条件的重写会让设备端缓存失效，把一次 60 MB 的上传塞进每一步的计时里。
        ''' </remarks>
        Public Sub SetGain(gain As Double)
            Call Matrix.ApplyGain(gain)

            gain = gain
        End Sub

        ''' <summary>当前 CSR 的权重统计。</summary>
        Public Function Statistics() As WeightStatistics
            Return WeightStatistics.FromMatrix(Synapses)
        End Function

        Public Function Describe() As String
            Dim stat As WeightStatistics = Statistics()

            Return $"{Units} neurons, nnz={Nnz} (csv rows={Triplets.CsvRows}), gain={Gain}, {stat}"
        End Function

    End Class

End Namespace
