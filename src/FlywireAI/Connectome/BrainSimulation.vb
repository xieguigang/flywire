Imports FlywireAI.Connectome.Network
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports Microsoft.VisualBasic.MachineLearning.TensorFlow
Imports std = System.Math

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

        ''' <summary>实际使用的张量计算后端名称 (CUDA / SIMD)。</summary>
        Public Property Backend As String

        ''' <summary>
        ''' 最近一步实际使用的 LIF 前向路径 (``Fused`` = 融合单步算子，``OpByOp`` = 逐算子)。
        ''' </summary>
        Public Property StepPath As String

        ''' <summary>回退到逐算子路径的时间步数 (0 表示整段仿真都走在融合路径上)。</summary>
        Public Property FallbackSteps As Integer

        ''' <summary>是否收集到了逐步统计 (KeepHistory=False 时不收集)。</summary>
        Public Property PerStepStatsAvailable As Boolean

        ''' <summary>设备常驻缓冲占用的字节数 (仅 GPU 有意义)。</summary>
        Public Property PinnedDeviceBytes As Long

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

            ' 把配置里的 GPU / 精度档位下发到层：融合单步、设备常驻精度、逐步轨迹开关。
            ' 必须在下一次 ResetState 之前设置 —— 常驻缓冲是在 ResetState 里分配与钉住的。
            layer.UseFusedStep = config.UseFusedStep
            layer.KeepHistory = config.KeepHistory
            layer.ResidentPrecision = config.ResidentPrecision

            Call layer.ResetState(1)

            Dim perStepSpikes As Double() = New Double(steps - 1) {}
            Dim perStepActive As Integer() = New Integer(steps - 1) {}
            Dim timer As Stopwatch = Stopwatch.StartNew()

            ' KeepHistory=True 时每步回读脉冲张量并统计（报告与既有行为一致）；
            ' 关闭后逐步统计留空 —— 计数由层内的设备端累加器负责，整段仿真零逐步回读。
            Dim collectPerStep As Boolean = config.KeepHistory

            ' 恒流编码（DirectCurrent）下每一步的外部电流完全相同：散射一次后复用同一份缓冲。
            ' 这不只是省一次 1.1 MB 分配 —— GPU 后端按「数组引用 + 版本号」缓存显存副本，
            ' 复用同一个张量意味着外部电流只在第一步上传一次（否则每步都要 1.1 MB H2D，
            ' 在 WDDM 上约 0.4 ms/步）。
            Dim sharedExt As Tensor = Nothing

            If isConstantSequence(sequence) Then
                sharedExt = scatter(sequence(0), stimulation.InputMap, units)
            End If

            For t As Integer = 0 To steps - 1
                ' 显式分支而不是 If(sharedExt, scatter(...))：后者会把散射写在表达式里，
                ' 一旦求值时机不如预期（例如被编译器改为"两个参数都求值"），
                ' 每步仍然会白白分配并散射一份 1.1 MB 的张量 —— 这类性能问题极难从代码上一眼看出来
                Dim ext As Tensor

                If sharedExt IsNot Nothing Then
                    ext = sharedExt
                Else
                    ext = scatter(sequence(t), stimulation.InputMap, units)
                End If
                Dim spikes As Tensor = layer.ForwardStep(ext)
                Dim stepSpikes As Double = 0
                Dim stepActive As Integer = 0

                If collectPerStep Then
                    ' 层内已把该步脉冲同步回主机（见 SparseLIFLayer.RecordStep），这里读到的是新值
                    Dim sd As Double() = spikes.Data

                    For i As Integer = 0 To sd.Length - 1
                        Dim v As Double = sd(i)

                        If v > 0 Then
                            stepSpikes += v
                            stepActive += 1
                        End If
                    Next

                    perStepSpikes(t) = stepSpikes
                    perStepActive(t) = stepActive
                End If

                If Not reporter Is Nothing AndAlso
                    ((t + 1) Mod config.ProgressEverySteps = 0 OrElse t = steps - 1) Then

                    Call reporter($"  step {t + 1}/{steps}: active={stepActive}, spikes={stepSpikes}, elapsed={timer.ElapsedMilliseconds} ms")
                End If
            Next

            ' 计数累加器：融合路径由设备内核逐步累加，逐算子路径由层内主机循环累加，
            ' 因此这里只需同步一次即可拿到 Σ_t S[t]（不再需要 O(T·N) 的主机求和）
            Call layer.SyncFromDevice()

            Dim counts As Double() = CType(layer.Counts.Data.Clone(), Double())

            timer.Stop()

            ' 把"实际走的路径"报出来：与"以为走了 GPU"之间的差别正是最难发现的性能问题
            Call BrainNetworkBuilder.report(
                reporter,
                $"  backend={GpuRuntime.BackendName}, path={layer.LastStepPath}, " &
                $"fallbackSteps={layer.FallbackSteps}, pinned={GpuRuntime.PinnedBytesDescription}, " &
                $"elapsed={timer.ElapsedMilliseconds} ms")

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
                .ElapsedMs = timer.ElapsedMilliseconds,
                .Backend = GpuRuntime.BackendName,
                .StepPath = layer.LastStepPath.ToString,
                .FallbackSteps = layer.FallbackSteps,
                .PerStepStatsAvailable = collectPerStep,
                .PinnedDeviceBytes = If(GpuRuntime.Backend Is Nothing, 0L, GpuRuntime.Backend.PinnedDeviceBytes)
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
                    ' 恒流：各步内容相同，共享一份缓冲（见 execute 中关于显存上传的说明）
                    Return SpikeEncoders.DirectCurrentEncode(input, steps, shareBuffer:=True)
                Case Else
                    Return SpikeEncoders.LatencyEncode(input, steps)
            End Select
        End Function

        ''' <summary>
        ''' 编码序列的每一步是否都是同一个张量（恒流编码的特征）。
        ''' </summary>
        Private Function isConstantSequence(sequence As List(Of Tensor)) As Boolean
            If sequence Is Nothing OrElse sequence.Count = 0 Then Return False

            For t As Integer = 1 To sequence.Count - 1
                If Not sequence(t) Is sequence(0) Then Return False
            Next

            Return True
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
