Imports FlywireAI.FAFBv783
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
        Public ReadOnly Property Gain As Double

        Friend Sub New(network As SpikingNetwork,
                       synapses As SparseMatrix,
                       triplets As SynapseTriplets,
                       baseValues As Double(),
                       structuralFanIn As Double(),
                       gain As Double)

            Me.Network = network
            Me.Synapses = synapses
            Me.Triplets = triplets
            Me.BaseValues = baseValues
            Me.StructuralFanIn = structuralFanIn
            Me.Units = synapses.Columns
            Me.Gain = gain
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
        Public Sub SetGain(gain As Double)
            Call SynapseTriplets.ApplyGain(Synapses, BaseValues, gain)
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

    ''' <summary>
    ''' 连接组的 CSR 权重矩阵 (结构归一化之后的单位权重)，可以在多种刺激方案之间复用。
    ''' </summary>
    Public Class ConnectomeMatrix

        Public ReadOnly Property Synapses As SparseMatrix
        Public ReadOnly Property BaseValues As Double()
        Public ReadOnly Property StructuralFanIn As Double()
        Public ReadOnly Property Triplets As SynapseTriplets

        Friend Sub New(synapses As SparseMatrix, baseValues As Double(), fanIn As Double(), triplets As SynapseTriplets)
            Me.Synapses = synapses
            Me.BaseValues = baseValues
            Me.StructuralFanIn = fanIn
            Me.Triplets = triplets
        End Sub

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

    ''' <summary>
    ''' 果蝇全脑 SNN 网络的装配入口。
    ''' </summary>
    Public Module BrainNetworkBuilder

        ''' <summary>
        ''' 构建连接组的 CSR 稀疏矩阵 (含重复 (pre, post) 合并与结构归一化)。
        ''' 
        ''' 该矩阵与刺激方案无关，可以被多种刺激方案的网络复用。
        ''' </summary>
        ''' <param name="triplets">由连接表构建出来的突触三元组。</param>
        ''' <param name="reporter">进度回调。</param>
        Public Function BuildMatrix(triplets As SynapseTriplets, Optional reporter As Action(Of String) = Nothing) As ConnectomeMatrix
            If triplets Is Nothing Then
                Throw New ArgumentNullException(NameOf(triplets))
            End If

            Call report(reporter, $"building CSR sparse matrix from {triplets.CsvRows} connection rows...")

            Dim synapses As SparseMatrix = triplets.ToSparseMatrix()

            Call report(reporter, $"merging duplicated (pre, post) pairs -> nnz = {synapses.NonZeros}")

            ' 结构归一化 (Σ|w| = 1 per post neuron)，并保存单位权重用于增益标定
            Dim fanIn As Double() = SynapseTriplets.NormalizeStructural(synapses)
            Dim baseValues As Double() = CType(synapses.Values.Clone(), Double())

            Return New ConnectomeMatrix(synapses, baseValues, fanIn, triplets)
        End Function

        ''' <summary>
        ''' 用既有的 CSR 矩阵装配一个稀疏递归 LIF 网络。
        ''' </summary>
        ''' <param name="config">仿真配置。</param>
        ''' <param name="matrix">连接组的 CSR 权重矩阵 (<see cref="BuildMatrix"/>)。</param>
        ''' <param name="stimulation">
        ''' 刺激方案；其 ``InputMap.Length`` 就是网络的 ``inputSize`` (输入特征数)，
        ''' 同时也是 <c>SpikingNetwork.AddSparseLayer</c> 所要求的注入映射。
        ''' </param>
        Public Function Assemble(config As SnnConfig,
                                 matrix As ConnectomeMatrix,
                                 stimulation As Stimulation,
                                 Optional reporter As Action(Of String) = Nothing) As BrainNetwork

            If config Is Nothing Then
                Throw New ArgumentNullException(NameOf(config))
            End If
            If matrix Is Nothing Then
                Throw New ArgumentNullException(NameOf(matrix))
            End If
            If stimulation Is Nothing Then
                Throw New ArgumentNullException(NameOf(stimulation))
            End If

            Dim gain As Double = If(config.GlobalGain > 0, config.GlobalGain, 1.0)

            Call SynapseTriplets.ApplyGain(matrix.Synapses, matrix.BaseValues, gain)

            Call report(reporter, $"assembling spiking network: neurons={matrix.Units}, input features={stimulation.Count}")

            Dim net As New SpikingNetwork(stimulation.Count, config.TimeSteps, config.Encoding)

            net.Rng = New Random(config.Seed)

            Call net.AddSparseLayer(matrix.Synapses, config.Beta, config.Threshold, config.ResetMode, stimulation.InputMap)

            Return New BrainNetwork(net, matrix.Synapses, matrix.Triplets, matrix.BaseValues, matrix.StructuralFanIn, gain)
        End Function

        ''' <summary>
        ''' 一步完成「CSR 矩阵构建 + 网络装配」。
        ''' </summary>
        Public Function Build(config As SnnConfig,
                              triplets As SynapseTriplets,
                              stimulation As Stimulation,
                              Optional reporter As Action(Of String) = Nothing) As BrainNetwork

            Dim matrix As ConnectomeMatrix = BuildMatrix(triplets, reporter)

            Return Assemble(config, matrix, stimulation, reporter)
        End Function

        Friend Sub report(reporter As Action(Of String), message As String)
            If Not reporter Is Nothing Then
                Call reporter(message)
            End If
        End Sub

    End Module
End Namespace
