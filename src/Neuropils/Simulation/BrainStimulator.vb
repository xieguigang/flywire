Imports System.Threading
Imports FlywireAI.Connectome
Imports FlywireAI.Connectome.Network
Imports FlywireAI.FAFBv783
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports Neuropils.Data
Imports tf = Microsoft.VisualBasic.MachineLearning.TensorFlow
Imports tfCompute = Microsoft.VisualBasic.MachineLearning.TensorFlow.Compute

Namespace Simulation

    ''' <summary>
    ''' 交互式电刺激的仿真引擎：把"点中一个神经元并注入电流"变成一次可回放的 SNN 仿真。
    ''' </summary>
    ''' <remarks>
    ''' <b>为什么分「装配」与「刺激」两段</b>
    ''' 
    ''' 一次全脑仿真的固定成本几乎都在读连接表（261 MB / 534 万行）与建 CSR 上，
    ''' 而这两件事与"刺激哪个神经元"无关。因此 <see cref="Prepare"/> 只在前台做一次
    ''' （后台线程，数百毫秒量级），之后每一次点击只需要：
    ''' 
    ''' 1. 用目标神经元建一个 ``inputSize = 1`` 的驱动方案（<c>Stimulation.CreateSingle</c>）；
    ''' 2. 装配一个极轻量的网络（``AddSparseLayer`` 只校验并持有 CSR 引用，不复制权重）；
    ''' 3. 跑 T 个时间步并取回逐步激活。
    ''' 
    ''' 如果没有这一步"装配与刺激分离"，每次点击都要重读 534 万行连接 —— 交互体验无从谈起。
    ''' 
    ''' <b>增益（gain）只在装配阶段标定一次</b>：标定用的是配置里的批量刺激方案
    ''' （5000 个神经元），而单击刺激只有 1 个输入特征 —— 用单点刺激去标定会把增益顶到上限
    ''' （因为目标活跃比例永远达不到）。标定一次、之后固定增益，也正好保证了
    ''' "不同点位 / 不同强度的多次刺激"是在同一套网络参数下比较的。
    ''' </remarks>
    Public Class BrainStimulator

        Private ReadOnly m_config As VisualizationConfig
        Private ReadOnly m_dataset As BrainDataset

        Private m_matrix As ConnectomeMatrix
        Private m_gain As Double = 1.0
        Private m_gpuRegistered As Boolean
        Private m_prepared As Boolean

        Public Sub New(config As VisualizationConfig, dataset As BrainDataset)
            If config Is Nothing Then Throw New ArgumentNullException(NameOf(config))
            If dataset Is Nothing Then Throw New ArgumentNullException(NameOf(dataset))

            m_config = config
            m_dataset = dataset
        End Sub

        ''' <summary>连接组与增益是否已经就绪。</summary>
        Public ReadOnly Property IsPrepared As Boolean
            Get
                Return m_prepared
            End Get
        End Property

        ''' <summary>标定得到的全局权重增益。</summary>
        Public ReadOnly Property Gain As Double
            Get
                Return m_gain
            End Get
        End Property

        ''' <summary>刺激所用的后端名称。</summary>
        Public ReadOnly Property BackendName As String
            Get
                Return GpuRuntime.BackendName
            End Get
        End Property

        ''' <summary>最后一次失败的原因（``Nothing`` 表示没有失败）。</summary>
        Public Property LastError As String = Nothing

        ''' <summary>
        ''' 装配连接组并标定一次增益。
        ''' </summary>
        ''' <remarks>
        ''' 会读 261 MB 的连接表并建 CSR（数百毫秒到数秒），因此必须在后台线程调用。
        ''' 重复调用是幂等的：已经就绪时直接返回。
        ''' </remarks>
        Public Function Prepare(reporter As Action(Of String), cancel As CancellationToken) As Boolean
            If m_prepared Then Return True

            Dim timer As Stopwatch = Stopwatch.StartNew()

            Try
                LastError = Nothing

                ' 1) 尝试注册 CUDA 后端。注册失败是预期内的分支（没有 NVIDIA 显卡 / NVRTC 缺失），
                '    此时仿真仍然在 CPU 上跑通，只是慢 —— 由界面如实展示后端名称。
                If m_config.UseGpu Then
                    m_gpuRegistered = GpuRuntime.TryRegister(m_config, reporter)

                    If Not m_gpuRegistered Then
                        Call report(reporter, $"CUDA 后端不可用，改用 CPU: {GpuRuntime.LastError}")
                    End If
                End If

                ThrowIfCancelled(cancel)

                ' 2) 连接 -> 稀疏三元组 -> CSR。连接列直接复用数据集里已经解析好的那一份
                '    (来自 msgpack 转储包，且已经按索引解析/过滤)，这里不再读任何 csv。
                Call report(reporter, "building the connectome from the loaded connection table ...")

                Dim triplets As SynapseTriplets = SynapseTriplets.BuildFromIndices(
                    m_dataset.Index,
                    m_dataset.Pre,
                    m_dataset.Post,
                    m_dataset.SynCount.Select(Function(x) CDbl(x)).ToArray,
                    m_dataset.ConnectionNtType,
                    m_dataset.ConnectionNeurotransmitters,
                    m_config.ExcitatoryGain,
                    m_config.InhibitoryGain)

                ThrowIfCancelled(cancel)

                m_matrix = BrainNetworkBuilder.BuildMatrix(triplets, reporter)

                Call report(reporter, $"connectome ready: {m_matrix} ({timer.ElapsedMilliseconds} ms)")

                ThrowIfCancelled(cancel)

                ' 3) 用批量刺激方案标定一次增益
                If m_config.GlobalGain > 0 Then
                    m_gain = m_config.GlobalGain

                    Call report(reporter, $"using the configured global gain: {m_gain}")
                Else
                    Dim calibration As Stimulation = Stimulation.Create(m_config, m_dataset.Index)
                    Dim probe As BrainNetwork = BrainNetworkBuilder.Assemble(m_config, m_matrix, calibration, reporter)

                    m_gain = BrainSimulation.CalibrateGain(probe, m_config, calibration, reporter)

                    ' 标定用的网络不再需要：常驻缓冲不参与 LRU 淘汰，必须显式归还
                    Call releaseDeviceBuffers(probe)
                End If

                ' 后续的单点刺激直接沿用标定结果（GlobalGain > 0 时 CalibrateGain 会跳过探针）
                m_config.GlobalGain = m_gain
                m_prepared = True

                Call report(reporter, $"stimulator ready: gain={m_gain}, backend={GpuRuntime.BackendName}, " &
                                      $"elapsed={timer.ElapsedMilliseconds} ms")

                Return True
            Catch ex As OperationCanceledException
                Throw
            Catch ex As Exception
                LastError = $"{ex.GetType().Name}: {ex.Message}"

                Call report(reporter, $"[ERROR] 刺激引擎装配失败: {LastError}")

                Return False
            End Try
        End Function

        ''' <summary>
        ''' 对指定神经元施加电刺激并运行一次完整仿真。
        ''' </summary>
        ''' <param name="neuron">目标神经元索引（电极中心）</param>
        ''' <param name="radiusNm">募集半径（纳米）：电极附近的神经元会被一起兴奋</param>
        ''' <param name="strength">注入电流强度</param>
        ''' <param name="holdMilliseconds">按住时长（仅用于回显与落盘）</param>
        ''' <param name="reporter">进度回调</param>
        ''' <remarks>
        ''' 必须在后台线程调用：即使 GPU 可用，一次仿真仍需数十毫秒，CPU 上更慢。
        ''' 
        ''' 刺激的是一群神经元而不是一个 —— 原因见 <c>Stimulation.CreatePatch</c> 的说明
        ''' （归一化权重下一个神经元无法点燃网络，真实电极也会募集一片组织）。
        ''' </remarks>
        Public Function Stimulate(neuron As Integer,
                                  radiusNm As Double,
                                  strength As Double,
                                  Optional holdMilliseconds As Long = 0,
                                  Optional reporter As Action(Of String) = Nothing) As StimulationReplay

            If Not m_prepared Then
                Throw New InvalidOperationException("刺激引擎尚未完成装配 (Prepare)")
            End If

            Dim wall As Stopwatch = Stopwatch.StartNew()
            Dim recruited As Integer() = Recruit(m_dataset, neuron, radiusNm)
            Dim rootId As Long = m_dataset.Index.GetRootId(neuron)
            Dim label As String = $"electric stimulation @ #{neuron} (root_id {rootId}), " &
                                  $"radius={radiusNm / 1000.0:F0} um, recruited={recruited.Length}, current={strength:F2}"
            Dim stimulation As Stimulation = Stimulation.CreatePatch(m_dataset.Index, recruited, strength, label)

            Call report(reporter, $"stimulating {stimulation.Label}")

            ' 被募集的神经元数量决定 inputSize（电极越大，注入的输入特征越多），
            ' 装配本身很轻：AddSparseLayer 只持有 CSR 引用，不复制权重
            Dim network As BrainNetwork = BrainNetworkBuilder.Assemble(m_config, m_matrix, stimulation, reporter)

            Try
                Dim result As BrainSimulationResult = BrainSimulation.Run(network, m_config, stimulation, m_gain, reporter)

                wall.Stop()

                Dim replay As StimulationReplay = build(network, result, stimulation, neuron, recruited.Length, radiusNm, strength, holdMilliseconds, wall.ElapsedMilliseconds)

                ' 分析回放：把同一次刺激沿逐算子路径再跑一遍，取回膜电位轨迹。
                ' 失败只是"曲线回退到放电率"，不应该让整次刺激失败。
                Try
                    Call analyzePotential(stimulation, replay, reporter)
                Catch ex As Exception
                    Call report(reporter, $"[warn] 膜电位分析回放失败（曲线回退到放电率）: {ex.Message}")
                End Try

                Call report(reporter, $"stimulation finished: {replay.Describe()}")

                Return replay
            Finally
                ' 每轮结束后归还常驻缓冲：常驻表不参与 LRU 淘汰，留着会一直占住显存
                Call releaseDeviceBuffers(network)
            End Try
        End Function

        ''' <summary>
        ''' 募集点击位置附近的神经元（电极电流的扩散范围）。
        ''' </summary>
        ''' <param name="dataset">数据集（提供三维坐标）</param>
        ''' <param name="neuron">电极中心神经元</param>
        ''' <param name="radiusNm">募集半径（纳米）</param>
        ''' <param name="maxNeurons">募集数量上限（电极中心兴奋最强，超出上限时保留最近的）</param>
        ''' <remarks>
        ''' 一次线性扫描 13 万个坐标（约 1 ms），因此界面可以在"按住"的过程中实时预览
        ''' "这一下会募集多少个神经元"，不需要预先建空间索引。
        ''' </remarks>
        Public Shared Function Recruit(dataset As BrainDataset,
                                      neuron As Integer,
                                      radiusNm As Double,
                                      Optional maxNeurons As Integer = DefaultMaxRecruit) As Integer()

            If dataset Is Nothing OrElse neuron < 0 OrElse neuron >= dataset.Units Then
                Return New Integer() {System.Math.Max(0, neuron)}
            End If
            If Not dataset.HasPosition(neuron) Then
                Return New Integer() {neuron}
            End If

            Dim positions As Double() = dataset.Positions
            Dim center As NeuronPosition = dataset.GetPosition(neuron)
            Dim limit As Double = radiusNm * radiusNm
            Dim hits As New List(Of Integer)(4096)

            For i As Integer = 0 To dataset.Units - 1
                Dim offset As Integer = i * 3

                If Double.IsNaN(positions(offset)) Then Continue For

                Dim dx As Double = positions(offset) - center.X
                Dim dy As Double = positions(offset + 1) - center.Y
                Dim dz As Double = positions(offset + 2) - center.Z

                If (dx * dx + dy * dy + dz * dz) <= limit Then
                    Call hits.Add(i)
                End If
            Next

            If hits.Count = 0 Then
                Return New Integer() {neuron}
            End If

            If hits.Count <= maxNeurons Then
                Return hits.ToArray()
            End If

            ' 超出上限：按距离取最近的一批（相当于把电极尖端之外的组织排除在外）
            Dim distances As Double() = New Double(hits.Count - 1) {}

            For i As Integer = 0 To hits.Count - 1
                Dim offset As Integer = hits(i) * 3
                Dim dx As Double = positions(offset) - center.X
                Dim dy As Double = positions(offset + 1) - center.Y
                Dim dz As Double = positions(offset + 2) - center.Z

                distances(i) = dx * dx + dy * dy + dz * dz
            Next

            Dim order As Integer() = Enumerable.Range(0, hits.Count) _
                .OrderBy(Function(k As Integer) distances(k)) _
                .Take(maxNeurons) _
                .ToArray()
            Dim recruited As Integer() = New Integer(order.Length - 1) {}

            For i As Integer = 0 To order.Length - 1
                recruited(i) = hits(order(i))
            Next

            Return recruited
        End Function

        ''' <summary>募集数量的默认上限。</summary>
        ''' <remarks>
        ''' 上限同时决定输入张量的宽度（每个被募集的神经元是一个输入特征）。
        ''' 实测（全脑坐标，密度约 0.004 个/μm³）：半径 25 μm 募集约 220 个，
        ''' 50 μm 约 2,500 个，100 μm 约 17,000 个 —— 如果上限压到 6000，
        ''' 按住时长的后半段就会失去意义（半径再大结果也一样）。
        ''' 25,000 让 25~140 μm 整个区间保持单调（140 μm 的理论值约 34,000，取上限），
        ''' 而散射与输入张量的代价仍然可以忽略 —— 实测 15,000 募集时单次仿真 80 ms，
        ''' 25,000 时约 120 ms，仍然是"点一下就能看到结果"的量级。
        ''' </remarks>
        Public Const DefaultMaxRecruit As Integer = 25000

#Region "response potential analysis"

        ''' <summary>
        ''' 分析回放：把同一次刺激沿<b>逐算子路径</b>再跑一遍，取回每个神经元的触发前膜电位。
        ''' </summary>
        ''' <param name="stimulation">刺激方案（与快速仿真完全相同的方案）</param>
        ''' <param name="replay">快速仿真的结果（响应神经元集合由它决定，膜电位写回它）</param>
        ''' <param name="reporter">进度回调</param>
        ''' <remarks>
        ''' <b>为什么要再跑一遍</b>：融合单步内核在设备内完成"泄漏积分 + 阈值触发 + 复位"，
        ''' 中间不落膜电位（<c>SparseLIFLayer.UHistory</c> 只在逐算子路径下产生，这是 SNN 库的
        ''' 既有约定）。而膜电位是唯一能反映"响应强度"的连续量：脉冲是 0/1，
        ''' 画出来只是一堆方波。
        ''' 
        ''' <b>为什么可信</b>：
        ''' <list type="bullet">
        '''   <item>两条路径在双精度档下数值等价（SNN 库有逐位一致实测），因此回放给出的就是
        '''         快速仿真内部的那条轨迹；</item>
        '''   <item>回放结束后会把它的脉冲序列与快速仿真<b>逐步逐神经元对齐</b>，
        '''         不一致的个数记进 <c>AnalysisMismatches</c> —— 曲线图上会明确标注，
        '''         不一致时不要采信膜电位。</item>
        ''' </list>
        ''' 
        ''' <b>为什么把后端切到 SIMD</b>：逐算子路径会为每个算子产生一次显存往返，
        ''' 在 GPU 上反而更慢；这条路径本来就是主机标量实现（与 GPU 逐位等价），
        ''' 所以分析阶段用 CPU 跑，跑完再把后端切回去。
        ''' </remarks>
        Private Sub analyzePotential(stimulation As Stimulation,
                                     replay As StimulationReplay,
                                     reporter As Action(Of String))

            Dim responders As Integer() = collectResponders(replay)

            If responders.Length = 0 Then
                Call report(reporter, "没有任何神经元响应，跳过膜电位分析回放")

                Return
            End If

            Dim timer As Stopwatch = Stopwatch.StartNew()
            Dim fused As Boolean = m_config.UseFusedStep
            Dim backend As tfCompute.ITensorCompute = tf.Tensor.computeKernel

            Try
                m_config.UseFusedStep = False
                tfCompute.SIMDTensor.Register()

                ' 用一份独立装配的网络做回放：不复用快速仿真的那份状态，
                ' 免得两次运行的计数/状态互相踩到
                Dim probe As BrainNetwork = BrainNetworkBuilder.Assemble(m_config, m_matrix, stimulation, Nothing)
                Dim result As BrainSimulationResult = BrainSimulation.Run(probe, m_config, stimulation, m_gain, Nothing)
                Dim layer As SparseLIFLayer = probe.Network.SparseLayer
                Dim potentials As List(Of tf.Tensor) = If(layer Is Nothing, Nothing, layer.UHistory)
                Dim spikes As List(Of tf.Tensor) = If(layer Is Nothing, Nothing, layer.SHistory)

                If potentials Is Nothing OrElse potentials.Count < replay.Steps Then
                    Call report(reporter, $"[warn] 分析回放只拿到 {If(potentials Is Nothing, 0, potentials.Count)}/{replay.Steps} 步膜电位，跳过")

                    Return
                End If

                Dim mismatches As Integer = countSpikeMismatches(replay, spikes)

                If mismatches > 0 Then
                    Call report(reporter, $"[warn] 分析回放的脉冲序列与快速仿真有 {mismatches} 处不一致，膜电位仅供参考")
                End If

                ' 只保留"有响应"神经元的轨迹：静止神经元占绝大多数，存下来只会白白撑大报告
                Dim matrix As Double()() = New Double(responders.Length - 1)() {}

                For k As Integer = 0 To responders.Length - 1
                    Dim row As Double() = New Double(replay.Steps - 1) {}

                    For t As Integer = 0 To replay.Steps - 1
                        row(t) = potentials(t).Data(responders(k))
                    Next

                    matrix(k) = row
                Next

                replay.ResponseNeurons = responders
                replay.ResponsePotential = matrix
                replay.ResponseSignal = "触发前膜电位 U (阈值 " & m_config.Threshold.ToString("G4") & ")"
                replay.AnalysisMs = timer.ElapsedMilliseconds
                replay.AnalysisMismatches = mismatches
                replay.AnalysisMatches = mismatches = 0

                Call report(reporter, $"membrane potential captured: {responders.Length:N0} neurons x {replay.Steps} steps, " &
                                      $"{timer.ElapsedMilliseconds} ms, spike mismatches={mismatches}")
            Finally
                m_config.UseFusedStep = fused
                tf.Tensor.computeKernel = backend
            End Try
        End Sub

        ''' <summary>快速仿真里至少发放过一次的神经元（也就是响应矩阵的行）。</summary>
        Private Shared Function collectResponders(replay As StimulationReplay) As Integer()
            Dim members As New List(Of Integer)(4096)

            If replay.Counts IsNot Nothing Then
                For i As Integer = 0 To replay.Counts.Length - 1
                    If replay.Counts(i) > 0 Then
                        Call members.Add(i)
                    End If
                Next
            End If

            Return members.ToArray()
        End Function

        ''' <summary>逐步比对两条路径的脉冲序列，返回不一致的神经元-步对数。</summary>
        Private Shared Function countSpikeMismatches(replay As StimulationReplay, spikes As List(Of tf.Tensor)) As Integer
            If spikes Is Nothing OrElse spikes.Count < replay.Steps Then
                Return replay.ActiveUnionCount
            End If

            Dim mismatches As Integer = 0
            Dim flag As Boolean() = New Boolean(replay.Units - 1) {}

            For t As Integer = 0 To replay.Steps - 1
                ' 变量不能叫 step：那是 VB 的保留字（For ... Step）
                Dim spikeStep As Double() = spikes(t).Data
                Dim active As Integer() = replay.StepActive(t)

                ' 快速仿真发放、分析回放没发放
                For i As Integer = 0 To active.Length - 1
                    If spikeStep(active(i)) = 0 Then mismatches += 1
                Next

                ' 分析回放发放、快速仿真没发放
                For i As Integer = 0 To spikeStep.Length - 1
                    If spikeStep(i) <> 0 Then flag(i) = True
                Next

                For i As Integer = 0 To active.Length - 1
                    flag(active(i)) = False
                Next

                For i As Integer = 0 To flag.Length - 1
                    If flag(i) Then
                        mismatches += 1
                        flag(i) = False
                    End If
                Next
            Next

            Return mismatches
        End Function

#End Region

#Region "replay assembly"

        Private Function build(network As BrainNetwork,
                               result As BrainSimulationResult,
                               stimulation As Stimulation,
                               neuron As Integer,
                               recruited As Integer,
                               radiusNm As Double,
                               strength As Double,
                               holdMilliseconds As Long,
                               wallMs As Long) As StimulationReplay

            Dim layer As SparseLIFLayer = network.Network.SparseLayer
            Dim steps As Integer = result.TimeSteps
            Dim active As Integer()() = New Integer(steps - 1)() {}

            For t As Integer = 0 To steps - 1
                active(t) = activationsOf(layer, t)
            Next

            Return New StimulationReplay With {
                .Label = stimulation.Label,
                .Neuron = neuron,
                .RootId = m_dataset.Index.GetRootId(neuron),
                .RecruitedNeurons = recruited,
                .RadiusNm = radiusNm,
                .Strength = strength,
                .HoldMilliseconds = holdMilliseconds,
                .Steps = steps,
                .Units = result.Units,
                .ActiveSteps = active,
                .SpikesPerStep = result.PerStepSpikes,
                .ActivePerStep = result.PerStepActive,
                .Counts = result.Counts,
                .TotalSpikes = result.TotalSpikes,
                .Gain = result.Gain,
                .Backend = result.Backend,
                .StepPath = result.StepPath,
                .FallbackSteps = result.FallbackSteps,
                .ElapsedMs = result.ElapsedMs,
                .WallMs = wallMs,
                .GeneratedAt = DateTime.Now
            }
        End Function

        ''' <summary>
        ''' 取第 <paramref name="stepIndex"/> 步被激活的神经元索引。
        ''' </summary>
        ''' <remarks>
        ''' 数据源是层内的逐步脉冲轨迹 <c>SHistory</c>（<c>KeepHistory = True</c> 时每步一份独立
        ''' 快照）：脉冲张量里大于 0 的分量就是这一步发放的神经元。
        ''' 之所以不用 <c>BrainSimulationResult</c>，是因为它只保留了逐步的<b>计数</b>，
        ''' 而回放需要具体是哪些神经元。
        ''' </remarks>
        Private Shared Function activationsOf(layer As SparseLIFLayer, stepIndex As Integer) As Integer()
            If layer Is Nothing OrElse layer.SHistory Is Nothing OrElse stepIndex >= layer.SHistory.Count Then
                Return New Integer() {}
            End If

            Dim spikes As Double() = layer.SHistory(stepIndex).Data
            Dim active As New List(Of Integer)(512)

            For i As Integer = 0 To spikes.Length - 1
                If spikes(i) <> 0 Then
                    Call active.Add(i)
                End If
            Next

            Return active.ToArray()
        End Function

        Private Shared Sub releaseDeviceBuffers(network As BrainNetwork)
            If network Is Nothing OrElse network.Network Is Nothing Then Return

            Dim layer As SparseLIFLayer = network.Network.SparseLayer

            If layer IsNot Nothing Then
                Try
                    Call layer.ReleaseDeviceBuffers()
                Catch ex As Exception
                    ' 归还失败不应影响结果：下一次 ResetState 会重新分配。
                    ' 这里写全 System.Diagnostics.Debug：SNN 命名空间下有同名的类型，
                    ' 简写会让编译器挑到实例成员而报错
                    System.Diagnostics.Debug.WriteLine($"unable to release the resident buffers: {ex.Message}")
                End Try
            End If
        End Sub

#End Region

        Private Shared Sub report(reporter As Action(Of String), message As String)
            If reporter Is Nothing Then Return

            Call reporter(message)
        End Sub

        Private Shared Sub ThrowIfCancelled(cancel As CancellationToken)
            If cancel.IsCancellationRequested Then
                Throw New OperationCanceledException(cancel)
            End If
        End Sub

    End Class

End Namespace
