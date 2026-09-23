Imports System.Diagnostics
Imports System.Linq
Imports std = System.Math
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports Microsoft.VisualBasic.MachineLearning.TensorFlow

Namespace Connectome

    ''' <summary>
    ''' 一次全脑脉冲仿真的结果。
    ''' </summary>
    Public Class BrainSimulationResult

        Public Property Mode As StimulationMode
        Public Property StimulationLabel As String
        Public Property Gain As Double
        Public Property TimeSteps As Integer

        ''' <summary>每个神经元的脉冲计数 (长度 N)。</summary>
        Public Property Counts As Double()

        ''' <summary>至少发放过一次的神经元索引。</summary>
        Public Property ActiveNeurons As Integer()

        ''' <summary>仿真窗口内的脉冲总数。</summary>
        Public Property TotalSpikes As Double

        ''' <summary>每个时间步的脉冲数。</summary>
        Public Property PerStepSpikes As Double()

        ''' <summary>每个时间步的活跃神经元数。</summary>
        Public Property PerStepActive As Integer()

        ''' <summary>仿真耗时 (毫秒)。</summary>
        Public Property ElapsedMs As Long

        ''' <summary>神经元的数量。</summary>
        Public Property Units As Integer

        ''' <summary>全局平均发放率 (每个神经元每步的脉冲数)。</summary>
        Public ReadOnly Property MeanFiringRate As Double
            Get
                If Units = 0 OrElse TimeSteps = 0 Then Return 0
                Return TotalSpikes / Units / TimeSteps
            End Get
        End Property

        ''' <summary>活跃神经元的比例。</summary>
        Public ReadOnly Property ActiveFraction As Double
            Get
                If Units = 0 Then Return 0
                Return ActiveNeurons.Length / CDbl(Units)
            End Get
        End Property

        ''' <summary>活跃神经元的平均发放率。</summary>
        Public ReadOnly Property MeanActiveRate As Double
            Get
                If ActiveNeurons.Length = 0 OrElse TimeSteps = 0 Then Return 0
                Return TotalSpikes / ActiveNeurons.Length / TimeSteps
            End Get
        End Property

        Public Function Describe() As String
            Return $"{Mode}: spikes={TotalSpikes}, active={ActiveNeurons.Length}/{Units} ({ActiveFraction:P2}), " &
                $"mean rate={MeanFiringRate:E3}/step, active rate={MeanActiveRate:F4}/step, gain={Gain}, T={TimeSteps}, {ElapsedMs} ms"
        End Function

    End Class

    ''' <summary>
    ''' 全脑 SNN 的仿真运行器：活动标定 + 逐时间步推进。
    ''' 
    ''' 这里没有直接调用 <c>SpikingNetwork.ForwardSpikes</c>，而是显式地逐时间步驱动
    ''' <c>SparseLIFLayer.ForwardStep</c> (与库内部 <c>ForwardSparse</c> 完全一致的语义)，
    ''' 从而能够打印每一个时间步的进度与活跃情况；两条路径的一致性由 demo 断言校验。
    ''' </summary>
    Public Module BrainSimulation

        ''' <summary>
        ''' 通过若干个短探针在候选增益之中挑选一个使活跃比例接近目标值的全局增益。
        ''' 
        ''' (配置了 <c>GlobalGain &gt; 0</c> 的时候直接采用配置值，不做标定)
        ''' </summary>
        Public Function CalibrateGain(network As BrainNetwork,
                                      config As SnnConfig,
                                      stimulation As Stimulation,
                                      Optional reporter As Action(Of String) = Nothing) As Double

            If config.GlobalGain > 0 Then
                Call BrainNetworkBuilder.report(reporter, $"using the configured global gain: {config.GlobalGain}")

                Return config.GlobalGain
            End If

            Call BrainNetworkBuilder.report(reporter, $"calibrating the global gain with {config.CalibrationProbeSteps} step probes...")

            Dim bestGain As Double = 1.0
            Dim bestScore As Double = Double.MaxValue

            For Each gain As Double In config.CalibrationCandidates
                Dim probe As BrainSimulationResult = execute(network, config, stimulation, gain, config.CalibrationProbeSteps, Nothing)

                Dim active As Double = probe.ActiveNeurons.Length / CDbl(network.Units)
                Dim score As Double = std.Abs(active - config.TargetActiveFraction)

                ' 完全静默的候选给一个额外的惩罚，避免选到不活动的网络
                If probe.TotalSpikes <= 0 Then
                    score += 1.0
                End If

                Call BrainNetworkBuilder.report(
                    reporter,
                    $"  gain={gain}: active={probe.ActiveNeurons.Length} ({active:P2}), spikes={probe.TotalSpikes}, {probe.ElapsedMs} ms")

                If score < bestScore Then
                    bestScore = score
                    bestGain = gain
                End If
            Next

            Call BrainNetworkBuilder.report(reporter, $"selected global gain = {bestGain} (active fraction ~ {config.TargetActiveFraction:P2})")

            Return bestGain
        End Function

        ''' <summary>
        ''' 以指定的全局增益运行 T 个时间步的全脑仿真。
        ''' </summary>
        Public Function Run(network As BrainNetwork,
                            config As SnnConfig,
                            stimulation As Stimulation,
                            gain As Double,
                            Optional reporter As Action(Of String) = Nothing) As BrainSimulationResult

            Call BrainNetworkBuilder.report(reporter, $"running simulation: T={config.TimeSteps}, gain={gain}, {stimulation}")

            Return execute(network, config, stimulation, gain, config.TimeSteps, reporter)
        End Function

        ''' <summary>
        ''' 逐时间步推进稀疏递归 LIF 网络 (batch = 1)。
        ''' </summary>
        Private Function execute(network As BrainNetwork,
                                 config As SnnConfig,
                                 stimulation As Stimulation,
                                 gain As Double,
                                 steps As Integer,
                                 reporter As Action(Of String)) As BrainSimulationResult

            Call network.SetGain(gain)

            Dim net As SpikingNetwork = network.Network
            Dim units As Integer = network.Units

            net.TimeSteps = steps
            net.Rng = New Random(config.Seed)

            Dim input As Tensor = stimulation.CreateInputTensor()
            Dim sequence As List(Of Tensor) = encode(net, input, steps)

            network.Synapses.GetType() ' 保持 matrix 引用 (无实际操作)

            Dim layer As SparseLIFLayer = net.SparseLayer

            If layer Is Nothing Then
                Throw New InvalidOperationException("网络尚未装配稀疏递归层")
            End If


            Call layer.ResetState(1)

            Dim counts As Double() = New Double(units - 1) {}
            Dim perStepSpikes As Double() = New Double(steps - 1) {}
            Dim perStepActive As Integer() = New Integer(steps - 1) {}
            Dim timer As Stopwatch = Stopwatch.StartNew()

            For t As Integer = 0 To steps - 1
                Dim ext As Tensor = scatter(sequence(t), stimulation.InputMap, units)
                Dim spikes As Tensor = layer.ForwardStep(ext)
                Dim sd As Double() = spikes.Data
                Dim stepSpikes As Double = 0
                Dim stepActive As Integer = 0

                For i As Integer = 0 To sd.Length - 1
                    Dim v As Double = sd(i)

                    If v > 0 Then
                        counts(i) += v
                        stepSpikes += v
                        stepActive += 1
                    End If
                Next

                perStepSpikes(t) = stepSpikes
                perStepActive(t) = stepActive

                If Not reporter Is Nothing AndAlso
                    ((t + 1) Mod config.ProgressEverySteps = 0 OrElse t = steps - 1) Then

                    Call reporter($"  step {t + 1}/{steps}: active={stepActive}, spikes={stepSpikes}, elapsed={timer.ElapsedMilliseconds} ms")
                End If
            Next

            timer.Stop()

            Dim active As Integer() = Enumerable _
                .Range(0, units) _
                .Where(Function(i) counts(i) > 0) _
                .ToArray

            Return New BrainSimulationResult With {
                .Mode = stimulation.Mode,
                .StimulationLabel = stimulation.Label,
                .Gain = gain,
                .TimeSteps = steps,
                .Units = units,
                .Counts = counts,
                .ActiveNeurons = active,
                .TotalSpikes = counts.Sum,
                .PerStepSpikes = perStepSpikes,
                .PerStepActive = perStepActive,
                .ElapsedMs = timer.ElapsedMilliseconds
            }
        End Function

        ''' <summary>
        ''' 与 <c>SpikingNetwork.Encode</c> 保持一致的输入编码。
        ''' </summary>
        Private Function encode(net As SpikingNetwork, input As Tensor, steps As Integer) As List(Of Tensor)
            Select Case net.Encoding
                Case SpikeEncoding.RateCoding
                    Return SpikeEncoders.RateEncode(input, steps, net.Rng)
                Case SpikeEncoding.DirectCurrent
                    Return SpikeEncoders.DirectCurrentEncode(input, steps)
                Case Else
                    Return SpikeEncoders.LatencyEncode(input, steps)
            End Select
        End Function

        ''' <summary>
        ''' 与 <c>SpikingNetwork.ScatterInput</c> 一致的注入映射 (batch = 1)。
        ''' </summary>
        Private Function scatter(spikes As Tensor, map As Integer(), units As Integer) As Tensor
            If map Is Nothing Then
                Return spikes
            End If

            Dim ext As New Tensor(1, units)
            Dim target As Double() = ext.Data
            Dim source As Double() = spikes.Data

            For i As Integer = 0 To map.Length - 1
                Dim v As Double = source(i)

                If v <> 0 Then
                    target(map(i)) += v
                End If
            Next

            ' 绕过索引器就地写入之后，声明主机数据已修改
            Call ext.MarkHostModified()

            Return ext
        End Function

    End Module
End Namespace
