Imports System.Diagnostics
Imports System.Linq
Imports System.Text
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports Microsoft.VisualBasic.MachineLearning.TensorFlow
Imports Microsoft.VisualBasic.MachineLearning.TensorFlow.Compute

Namespace Connectome

    ''' <summary>
    ''' 某个档位参与验收的方式。
    ''' </summary>
    Public Enum GpuBenchmarkGate

        ''' <summary>只报告，不参与硬断言（例如单精度常驻档）。</summary>
        Report = 0

        ''' <summary>必须与<b>同配置</b>的 CPU 基准逐位一致。</summary>
        BitExact = 1

        ''' <summary>逐位一致 + 必须达到加速比门槛（"显著快于 CPU"的档位）。</summary>
        BitExactAndSpeedup = 2

    End Enum

    ''' <summary>
    ''' 基准中的一个运行档位（后端 / 精度 / 是否保留逐步轨迹）。
    ''' </summary>
    ''' <remarks>
    ''' 对拍必须<b>同配置</b>：保留逐步轨迹的档位每步要回读一次脉冲张量（GPU 下这是一次
    ''' 同步 D2H 拷贝），拿它去和"不保留轨迹的 CPU 档位"比加速比是不公平的
    ''' （等于让 GPU 少做一截工作还去比速度）。因此基准按 <see cref="KeepHistory"/>
    ''' 为每个 GPU 档位匹配一个同配置的 CPU 基准。
    ''' </remarks>
    Public Class GpuBenchmarkMode

        ''' <summary>档位名称（写入报告与 csv）。</summary>
        Public Property Name As String

        ''' <summary>是否使用 CUDA 后端。</summary>
        Public Property UseGpu As Boolean

        ''' <summary>设备常驻状态的精度档位。</summary>
        Public Property Precision As LifResidentPrecision = LifResidentPrecision.Double64

        ''' <summary>是否保留逐步脉冲轨迹（False = 整段仿真零逐步回读）。</summary>
        Public Property KeepHistory As Boolean = True

        ''' <summary>该档位参与验收的方式。</summary>
        Public Property Gate As GpuBenchmarkGate = GpuBenchmarkGate.Report

        Public Overrides Function ToString() As String
            Return $"{Name} (gpu={UseGpu}, precision={Precision}, keepHistory={KeepHistory}, gate={Gate})"
        End Function

    End Class

    ''' <summary>单个档位的运行记录。</summary>
    Public Class GpuBenchmarkEntry

        Public Property Mode As GpuBenchmarkMode
        Public Property Backend As String
        Public Property StepPath As String
        Public Property FallbackSteps As Integer

        ''' <summary>仿真步循环耗时 (毫秒)，<b>不含</b>网络装配与常驻缓冲分配。</summary>
        ''' <remarks>
        ''' 多次重复测量时取最小值（稳态口径）。首次运行额外包含把整张连接表上传到显存
        ''' 的一次性开销（nnz = 373 万时约 60 MB，WDDM 上约 10 ms），见 <see cref="ColdMs"/>。
        ''' </remarks>
        Public Property ElapsedMs As Long

        ''' <summary>
        ''' 首次运行的仿真步循环耗时 (毫秒)：包含「连接表首次上传显存」这类一次性开销。
        ''' </summary>
        Public Property ColdMs As Long

        ''' <summary>整个档位的墙钟耗时 (毫秒)，含装配 / 常驻分配 / 释放。</summary>
        Public Property WallMs As Long

        Public Property PerStepMs As Double

        ''' <summary>相对<b>同配置</b> CPU 基准的加速比。</summary>
        Public Property Speedup As Double

        ''' <summary>用于计算 <see cref="Speedup"/> 的 CPU 基准档位名称。</summary>
        Public Property BaselineName As String

        Public Property TotalSpikes As Double
        Public Property ActiveNeurons As Integer
        Public Property Units As Integer
        Public Property PinnedBytes As Long

        ''' <summary>与同配置 CPU 基准逐神经元脉冲计数的最大绝对偏差。</summary>
        Public Property MaxAbsDelta As Double

        ''' <summary>与同配置 CPU 基准脉冲计数不同的神经元数量。</summary>
        Public Property DifferingNeurons As Integer

        ''' <summary>额外的诊断说明（跳过原因等）。</summary>
        Public Property Note As String

        ''' <summary>是否与 CPU 基准逐位一致。</summary>
        Public ReadOnly Property BitExact As Boolean
            Get
                Return DifferingNeurons = 0
            End Get
        End Property

        ''' <summary>该档位是否被跳过（例如 GPU 不可用）。</summary>
        Public ReadOnly Property Skipped As Boolean
            Get
                Return ElapsedMs <= 0
            End Get
        End Property

        Public Overrides Function ToString() As String
            If Skipped Then
                Return $"{Mode.Name}: skipped ({Note})"
            End If

            Return $"{Mode.Name}: {ElapsedMs} ms ({PerStepMs:F3} ms/step), speedup={Speedup:F2}x vs {BaselineName}, " &
                $"spikes={TotalSpikes}, active={ActiveNeurons}/{Units}, maxΔ={MaxAbsDelta:E3}, diffNeurons={DifferingNeurons}"
        End Function

    End Class

    ''' <summary>基准的完整结果。</summary>
    Public Class GpuBenchmarkReport

        ''' <summary>CPU 基准档位（按"是否保留逐步轨迹"索引）。</summary>
        Public Property Baselines As List(Of GpuBenchmarkEntry)

        Public Property Entries As List(Of GpuBenchmarkEntry)

        ''' <summary>落盘的 csv 文件路径。</summary>
        Public Property CsvFile As String

        ''' <summary>参与硬断言的档位是否全部达标。</summary>
        Public Property Passed As Boolean

        ''' <summary>加速比门槛。</summary>
        Public Property SpeedupGate As Double

        ''' <summary>需要逐位一致的档位。</summary>
        Public ReadOnly Property BitExactEntries As GpuBenchmarkEntry()
            Get
                Return filter(GpuBenchmarkGate.BitExact)
            End Get
        End Property

        ''' <summary>需要达到加速比门槛的档位。</summary>
        Public ReadOnly Property SpeedupEntries As GpuBenchmarkEntry()
            Get
                Return filter(GpuBenchmarkGate.BitExactAndSpeedup)
            End Get
        End Property

        Private Function filter(gate As GpuBenchmarkGate) As GpuBenchmarkEntry()
            If Entries Is Nothing Then Return New GpuBenchmarkEntry() {}

            Return Entries _
                .Where(Function(e) e.Mode.UseGpu AndAlso Not e.Skipped AndAlso e.Mode.Gate >= gate) _
                .ToArray
        End Function

        Public Function Describe() As String
            Dim sb As New StringBuilder()

            Call sb.AppendLine("mode                             backend  hist  elapsed   cold     ms/step  speedup           maxΔ       diffN")
            Call sb.AppendLine("---------------------------------------------------------------------------------------------------------------")

            For Each entry As GpuBenchmarkEntry In Entries
                Call sb.AppendFormat("{0,-32} {1,-8} {2,-5} ",
                                     entry.Mode.Name,
                                     entry.Backend,
                                     entry.Mode.KeepHistory.ToString)

                If entry.Skipped Then
                    Call sb.AppendLine($"skipped: {entry.Note}")
                Else
                    Call sb.AppendFormat("{0,7} ms {1,5} ms {2,8:F3} {3,7:F2}x {4,-11} {5,9:E3} {6,5}",
                                         entry.ElapsedMs,
                                         entry.ColdMs,
                                         entry.PerStepMs,
                                         entry.Speedup,
                                         If(entry.BaselineName, "-"),
                                         entry.MaxAbsDelta,
                                         entry.DifferingNeurons)
                    Call sb.AppendLine()
                End If
            Next

            Call sb.AppendLine()
            Call sb.AppendLine($"speedup gate: {SpeedupGate:F2}x (同一 keepHistory 配置下对比), passed: {Passed}")
            Call sb.AppendLine("elapsed = 稳态 (多次重复取最小)，cold = 首次运行 (含连接表首次上传显存)")

            Return sb.ToString
        End Function

    End Class

    ''' <summary>
    ''' 全脑 CPU / GPU 对拍与加速比基准。
    ''' </summary>
    ''' <remarks>
    ''' 设计要点：
    ''' <list type="bullet">
    '''   <item><b>同一连接组、同一刺激、同一种子</b>：所有档位共享同一个 CSR 矩阵与
    '''         <see cref="Stimulation"/> 对象，逐神经元脉冲计数因此可以直接对拍；</item>
    '''   <item><b>对拍同配置</b>：GPU 档位与"同样是否保留逐步轨迹"的 CPU 档位比较，
    '''         否则会把 CPU 侧的额外工作量算成 GPU 的加速；</item>
    '''   <item><b>执行顺序固定为"先 CPU 档、再 GPU 档"</b>：CPU 档若在 GPU 档之后运行，
    '''         层内已钉住的显存缓冲会失去同步通道（切到 CPU 后端后 <c>UnpinDevice</c>
    '''         落到 CPU 实现上就是空操作），顺序不能颠倒；</item>
    '''   <item><b>每个档位重复测量取最小值</b>：单次运行会被 GC 停顿污染
    '''         （实测同配置两次可差 30% 以上），取最小值是对"稳态性能"更稳的估计；</item>
    '''   <item>每个档位使用<b>独立</b>的 <see cref="BrainNetwork"/>（共享 CSR），
    '''         并在该档位结束时立即释放常驻显存。</item>
    ''' </list>
    ''' </remarks>
    Public Module GpuBenchmark

        ''' <summary>GPU 档位相对 CPU 基准的最低加速比（验收门槛）。</summary>
        ''' <remarks>
        ''' 3x 是保守门槛：本机（RTX A4000 / WDDM）实测"融合 + 双精度常驻 + 零逐步回读"
        ''' 档位为 5~6x。门槛故意留有余量，避免把机器负载波动当成回归。
        ''' </remarks>
        Public Const DefaultSpeedupGate As Double = 3.0

        ''' <summary>每个档位的重复测量次数（取最小耗时，抑制 GC 停顿带来的抖动）。</summary>
        ''' <remarks>
        ''' 单次测量会被托管堆的停顿污染：实测同一配置的两次运行可差 30% 以上
        ''' （CPU 侧每步要分配一份 <c>[1, N]</c> 缓冲，属于大对象堆分配）。
        ''' "取多次的最小值"是对稳态性能更稳的估计，也让 CPU / GPU 的比较更可复现。
        ''' </remarks>
        Public Property Repeats As Integer
            Get
                If _repeats <= 0 Then Return DefaultRepeats

                Return _repeats
            End Get
            Set(value As Integer)
                _repeats = value
            End Set
        End Property

        Private _repeats As Integer = DefaultRepeats

        ''' <summary>默认重复次数。</summary>
        Public Const DefaultRepeats As Integer = 3

        ''' <summary>档位 → 脉冲计数（对拍用；不污染报告的公开契约）。</summary>
        Private ReadOnly _counts As New Dictionary(Of GpuBenchmarkEntry, Double())

        ''' <summary>
        ''' 默认的档位组合：两个 CPU 基准（有/无逐步轨迹）+ 对应的 GPU 档位 + 单精度报告档。
        ''' </summary>
        ''' <remarks>
        ''' 分三对：
        ''' <list type="bullet">
        '''   <item>性能档（``no history``）：整段仿真零逐步回读 —— GPU 的<b>加速比门槛</b>落在这一对上；</item>
        '''   <item>报告档（``history``）：保留逐步轨迹（栅格图 / 逐步活跃曲线）—— 只做逐位对拍；</item>
        '''   <item>最快档（``fp32``）：单精度常驻状态，只如实报告偏差，不参与断言。</item>
        ''' </list>
        ''' 保留逐步轨迹的档位每步都要回读一次脉冲张量（GPU 下是一次同步 D2H 拷贝，
        ''' WDDM 上 1.1 MB 约 0.4 ms），因此它的"加速比"天然低于零回读档 ——
        ''' 这也是为什么门槛只落在同配置的性能档上。
        ''' </remarks>
        Public Function DefaultModes() As GpuBenchmarkMode()
            Dim perfBaseline As New GpuBenchmarkMode With {
                .Name = "CPU (fp64, no history)",
                .UseGpu = False,
                .Precision = LifResidentPrecision.Double64,
                .KeepHistory = False
            }
            Dim perfGpu As New GpuBenchmarkMode With {
                .Name = "GPU (fp64, no history)",
                .UseGpu = True,
                .Precision = LifResidentPrecision.Double64,
                .KeepHistory = False,
                .Gate = GpuBenchmarkGate.BitExactAndSpeedup
            }
            Dim historyBaseline As New GpuBenchmarkMode With {
                .Name = "CPU (fp64, history)",
                .UseGpu = False,
                .Precision = LifResidentPrecision.Double64,
                .KeepHistory = True
            }
            Dim historyGpu As New GpuBenchmarkMode With {
                .Name = "GPU (fp64, history)",
                .UseGpu = True,
                .Precision = LifResidentPrecision.Double64,
                .KeepHistory = True,
                .Gate = GpuBenchmarkGate.BitExact
            }
            Dim fp32Gpu As New GpuBenchmarkMode With {
                .Name = "GPU (fp32, history)",
                .UseGpu = True,
                .Precision = LifResidentPrecision.Single32,
                .KeepHistory = True,
                .Gate = GpuBenchmarkGate.Report
            }

            Return {perfBaseline, perfGpu, historyBaseline, historyGpu, fp32Gpu}
        End Function

        ''' <summary>
        ''' 在给定的连接组上依次运行各档位，产出对拍 / 加速比报告。
        ''' </summary>
        ''' <param name="config">仿真配置；本函数会临时改写其中的 GPU / 精度字段并在结束后还原。</param>
        ''' <param name="matrix">已构建好的 CSR 连接组（各档位共享）。</param>
        ''' <param name="stimulation">刺激方案（各档位共享，保证输入一致）。</param>
        ''' <param name="gain">全局权重增益（各档位共享）。</param>
        ''' <param name="modes">档位组合；<c>Nothing</c> 表示使用 <see cref="DefaultModes"/>。</param>
        ''' <param name="reporter">日志回调。</param>
        Public Function Run(config As SnnConfig,
                            matrix As ConnectomeMatrix,
                            stimulation As Stimulation,
                            gain As Double,
                            Optional modes As GpuBenchmarkMode() = Nothing,
                            Optional reporter As Action(Of String) = Nothing) As GpuBenchmarkReport

            If config Is Nothing Then Throw New ArgumentNullException(NameOf(config))
            If matrix Is Nothing Then Throw New ArgumentNullException(NameOf(matrix))
            If stimulation Is Nothing Then Throw New ArgumentNullException(NameOf(stimulation))

            If modes Is Nothing Then modes = DefaultModes()

            ' 上一轮基准留下的计数数组不再需要（每个约 1.1 MB）
            SyncLock _counts
                _counts.Clear()
            End SyncLock

            Dim savedUseGpu = config.UseGpu
            Dim savedPrecision = config.ResidentPrecision
            Dim savedHistory = config.KeepHistory

            Dim entries As New List(Of GpuBenchmarkEntry)()
            Dim baselines As New List(Of GpuBenchmarkEntry)()
            Dim gpuReady As Boolean = GpuRuntime.IsRegistered

            Try
                For Each mode As GpuBenchmarkMode In modes
                    If mode.UseGpu AndAlso Not gpuReady Then
                        Dim skipped As New GpuBenchmarkEntry With {
                            .Mode = mode,
                            .Backend = GpuRuntime.BackendName,
                            .StepPath = "-",
                            .Note = If(String.IsNullOrWhiteSpace(GpuRuntime.LastError),
                                       "CUDA backend is not available",
                                       GpuRuntime.LastError)
                        }

                        Call report(reporter, $"  [{mode.Name}] skipped: {skipped.Note}")
                        Call entries.Add(skipped)

                        Continue For
                    End If

                    Call report(reporter, $"  [{mode.Name}] running...")

                    Dim entry As GpuBenchmarkEntry = runMode(config, matrix, stimulation, gain, mode, reporter)

                    Call entries.Add(entry)

                    If Not mode.UseGpu Then
                        Call baselines.Add(entry)
                    End If
                Next
            Finally
                config.UseGpu = savedUseGpu
                config.ResidentPrecision = savedPrecision
                config.KeepHistory = savedHistory
            End Try

            ' 对拍与加速比：为每个 GPU 档位匹配"同配置"（KeepHistory 相同）的 CPU 基准
            For Each entry As GpuBenchmarkEntry In entries
                If entry.Skipped Then
                    Continue For
                End If
                If Not entry.Mode.UseGpu Then
                    entry.Speedup = 1.0
                    Continue For
                End If

                Dim baseline As GpuBenchmarkEntry = matchBaseline(baselines, entry.Mode)

                If baseline Is Nothing Then
                    entry.BaselineName = "-"
                    entry.MaxAbsDelta = Double.NaN
                    entry.DifferingNeurons = -1
                    Continue For
                End If

                entry.BaselineName = baseline.Mode.Name
                entry.Speedup = baseline.ElapsedMs / CDbl(entry.ElapsedMs)

                Dim delta = compareCounts(baseline, entry)

                entry.MaxAbsDelta = delta.MaxDiff
                entry.DifferingNeurons = delta.Differing
            Next

            Dim summary As New GpuBenchmarkReport With {
                .Baselines = baselines,
                .Entries = entries,
                .SpeedupGate = DefaultSpeedupGate
            }

            Dim passed As Boolean = baselines.Count > 0

            For Each entry As GpuBenchmarkEntry In summary.BitExactEntries
                If entry.DifferingNeurons <> 0 OrElse entry.MaxAbsDelta > 1.0E-9 Then
                    passed = False
                End If
            Next

            For Each entry As GpuBenchmarkEntry In summary.SpeedupEntries
                If entry.Speedup < DefaultSpeedupGate Then
                    passed = False
                End If
            Next

            summary.Passed = passed
            summary.CsvFile = writeCsv(config, summary)

            Return summary
        End Function

        ''' <summary>为 GPU 档位匹配同配置的 CPU 基准（找不到时退回第一个 CPU 档位）。</summary>
        Private Function matchBaseline(baselines As List(Of GpuBenchmarkEntry), mode As GpuBenchmarkMode) As GpuBenchmarkEntry
            Dim exact As GpuBenchmarkEntry = baselines _
                .FirstOrDefault(Function(b) b.Mode.KeepHistory = mode.KeepHistory)

            If exact IsNot Nothing Then Return exact
            If baselines.Count = 0 Then Return Nothing

            Return baselines(0)
        End Function

        ''' <summary>运行单个档位（内部负责切换后端、下发配置与释放常驻缓冲）。</summary>
        Private Function runMode(config As SnnConfig,
                                 matrix As ConnectomeMatrix,
                                 stimulation As Stimulation,
                                 gain As Double,
                                 mode As GpuBenchmarkMode,
                                 reporter As Action(Of String)) As GpuBenchmarkEntry

            Dim repeats As Integer = System.Math.Max(1, GpuBenchmark.Repeats)
            Dim best As GpuBenchmarkEntry = Nothing
            Dim first As GpuBenchmarkEntry = Nothing

            For attempt As Integer = 1 To repeats
                Dim current As GpuBenchmarkEntry = runOnce(config, matrix, stimulation, gain, mode, attempt, reporter)

                If first Is Nothing Then first = current
                If best Is Nothing OrElse current.ElapsedMs < best.ElapsedMs Then
                    best = current
                End If
            Next

            best.ColdMs = first.ElapsedMs

            If repeats > 1 Then
                Call report(reporter, $"    best of {repeats}: {best.ElapsedMs} ms (cold: {best.ColdMs} ms)")
            End If

            Return best
        End Function

        ''' <summary>运行单个档位一次。</summary>
        Private Function runOnce(config As SnnConfig,
                                 matrix As ConnectomeMatrix,
                                 stimulation As Stimulation,
                                 gain As Double,
                                 mode As GpuBenchmarkMode,
                                 attempt As Integer,
                                 reporter As Action(Of String)) As GpuBenchmarkEntry

            Call applyBackend(mode, reporter)

            ' 配置下发：层会在下一次 ResetState 时据此分配 / 钉住常驻缓冲
            config.UseGpu = mode.UseGpu
            config.ResidentPrecision = mode.Precision
            config.KeepHistory = mode.KeepHistory

            Dim wall As Stopwatch = Stopwatch.StartNew()
            Dim network As BrainNetwork = BrainNetworkBuilder.Assemble(config, matrix, stimulation, Nothing)
            Dim layer As SparseLIFLayer = network.Network.SparseLayer
            Dim result As BrainSimulationResult = BrainSimulation.Run(network, config, stimulation, gain, Nothing)

            Dim entry As New GpuBenchmarkEntry With {
                .Mode = mode,
                .Backend = result.Backend,
                .StepPath = result.StepPath,
                .FallbackSteps = result.FallbackSteps,
                .ElapsedMs = result.ElapsedMs,
                .PerStepMs = If(config.TimeSteps > 0, result.ElapsedMs / CDbl(config.TimeSteps), 0),
                .TotalSpikes = result.TotalSpikes,
                .ActiveNeurons = result.ActiveNeurons.Length,
                .Units = result.Units,
                .PinnedBytes = result.PinnedDeviceBytes,
                .Note = $"path={result.StepPath}, fallback={result.FallbackSteps}, pinned={result.PinnedDeviceBytes:N0} B"
            }

            ' 立即归还常驻显存（常驻缓冲不参与 LRU 淘汰，会挤压后续档位）
            If mode.UseGpu Then
                Call layer.ReleaseDeviceBuffers()
            End If

            wall.Stop()
            entry.WallMs = wall.ElapsedMilliseconds

            ' 只有最快那一次会被保留，但计数需要每次覆盖（同一档位的结果应当完全相同）
            Call storeCounts(entry, result.Counts)
            Call report(reporter, $"    [{attempt}] {entry}")

            Return entry
        End Function

        ''' <summary>把张量后端切换为该档位对应的实现。</summary>
        Private Sub applyBackend(mode As GpuBenchmarkMode, reporter As Action(Of String))
            If mode.UseGpu Then
                Dim backend = GpuRuntime.Backend

                If backend Is Nothing Then
                    Throw New InvalidOperationException("CUDA 后端尚未注册，无法运行 GPU 档位")
                End If

                ' 切回该实例（切换后端不会回收它持有的显存）
                SyncLock Tensor.SyncRoot
                    Tensor.computeKernel = backend
                End SyncLock
            Else
                SIMDTensor.Register()
            End If

            Call report(reporter, $"    backend = {GpuRuntime.BackendName}")
        End Sub

        ''' <summary>逐神经元脉冲计数对拍。</summary>
        Private Function compareCounts(baseline As GpuBenchmarkEntry, target As GpuBenchmarkEntry) As (MaxDiff As Double, Differing As Integer)
            Dim a As Double() = getCounts(baseline)
            Dim b As Double() = getCounts(target)

            ' 计数缺失 / 形状不一致属于结构性失败：用 -1 明确表达"无法对拍"
            If a Is Nothing OrElse b Is Nothing OrElse a.Length <> b.Length Then
                Return (Double.NaN, -1)
            End If

            Dim maxDiff As Double = 0
            Dim differing As Integer = 0

            For i As Integer = 0 To a.Length - 1
                Dim d As Double = System.Math.Abs(a(i) - b(i))

                If d > 0 Then
                    differing += 1
                End If
                If d > maxDiff Then
                    maxDiff = d
                End If
            Next

            Return (maxDiff, differing)
        End Function

        Private Sub storeCounts(entry As GpuBenchmarkEntry, counts As Double())
            SyncLock _counts
                _counts(entry) = counts
            End SyncLock
        End Sub

        Private Function getCounts(entry As GpuBenchmarkEntry) As Double()
            SyncLock _counts
                Dim counts As Double() = Nothing

                If _counts.TryGetValue(entry, counts) Then
                    Return counts
                End If

                Return Nothing
            End SyncLock
        End Function

        ''' <summary>把基准结果落盘为 csv（供归档与后续分析）。</summary>
        Private Function writeCsv(config As SnnConfig, report As GpuBenchmarkReport) As String
            Dim dir As String = config.GetOutputDir()

            Call IO.Directory.CreateDirectory(dir)

            Dim file As String = IO.Path.Combine(dir, "gpu_benchmark.csv")
            Dim sb As New StringBuilder()

            Call sb.AppendLine("mode,backend,use_gpu,precision,keep_history,gate,step_path,fallback_steps,elapsed_ms,cold_ms,wall_ms,per_step_ms,speedup,baseline,total_spikes,active_neurons,units,max_abs_delta,diff_neurons,pinned_bytes,note")

            For Each entry As GpuBenchmarkEntry In report.Entries
                Call sb.Append(quote(entry.Mode.Name)).Append(",")
                Call sb.Append(quote(entry.Backend)).Append(",")
                Call sb.Append(If(entry.Mode.UseGpu, "true", "false")).Append(",")
                Call sb.Append(entry.Mode.Precision.ToString).Append(",")
                Call sb.Append(If(entry.Mode.KeepHistory, "true", "false")).Append(",")
                Call sb.Append(entry.Mode.Gate.ToString).Append(",")
                Call sb.Append(quote(entry.StepPath)).Append(",")
                Call sb.Append(entry.FallbackSteps).Append(",")
                Call sb.Append(entry.ElapsedMs).Append(",")
                Call sb.Append(entry.ColdMs).Append(",")
                Call sb.Append(entry.WallMs).Append(",")
                Call sb.Append(entry.PerStepMs.ToString("F4")).Append(",")
                Call sb.Append(entry.Speedup.ToString("F4")).Append(",")
                Call sb.Append(quote(entry.BaselineName)).Append(",")
                Call sb.Append(entry.TotalSpikes.ToString("F0")).Append(",")
                Call sb.Append(entry.ActiveNeurons).Append(",")
                Call sb.Append(entry.Units).Append(",")
                Call sb.Append(entry.MaxAbsDelta.ToString("E3")).Append(",")
                Call sb.Append(entry.DifferingNeurons).Append(",")
                Call sb.Append(entry.PinnedBytes).Append(",")
                Call sb.Append(quote(entry.Note)).AppendLine()
            Next

            ' 元信息：把验收门槛与结论一并落盘，避免报告脱离上下文
            Call sb.AppendLine()
            Call sb.AppendLine($"{quote("speedup_gate")},{report.SpeedupGate:F2}")
            Call sb.AppendLine($"{quote("passed")},{If(report.Passed, "true", "false")}")
            Call sb.AppendLine($"{quote("repeats")},{Repeats}")
            Call sb.AppendLine($"{quote("time_steps")},{config.TimeSteps}")
            Call sb.AppendLine($"{quote("global_gain")},{config.GlobalGain}")
            Call sb.AppendLine($"{quote("seed")},{config.Seed}")
            Call sb.AppendLine($"{quote("backend")},{GpuRuntime.BackendName}")
            Call sb.AppendLine($"{quote("units")},{If(report.Baselines Is Nothing OrElse report.Baselines.Count = 0, 0, report.Baselines(0).Units)}")

            Call IO.File.WriteAllText(file, sb.ToString, Encoding.UTF8)

            Return file
        End Function

        Private Function quote(value As String) As String
            If value Is Nothing Then Return """"""

            Return """" & value.Replace("""", """""") & """"
        End Function

        Private Sub report(reporter As Action(Of String), message As String)
            If reporter Is Nothing Then Return

            Call reporter(message)
        End Sub

    End Module

End Namespace
