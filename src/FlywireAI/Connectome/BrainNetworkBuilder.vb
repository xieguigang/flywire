Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork

Namespace Connectome.Network

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

            ' 幂等应用增益：权重没变就不要让设备端缓存失效（详见 ConnectomeMatrix.ApplyGain）
            Call matrix.ApplyGain(gain)

            Call report(reporter, $"assembling spiking network: neurons={matrix.Units}, input features={stimulation.Count}")

            Dim net As New SpikingNetwork(stimulation.Count, config.TimeSteps, config.Encoding)

            net.Rng = New Random(config.Seed)

            Call net.AddSparseLayer(matrix.Synapses, config.Beta, config.Threshold, config.ResetMode, stimulation.InputMap)

            Return New BrainNetwork(net, matrix, matrix.Synapses, matrix.Triplets, matrix.BaseValues, matrix.StructuralFanIn, gain)
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