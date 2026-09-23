Imports System.Diagnostics
Imports System.Linq
Imports System.Text
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports Microsoft.VisualBasic.MachineLearning.TensorFlow
Imports Microsoft.VisualBasic.MachineLearning.TensorFlow.Compute

Namespace Connectome

    ''' <summary>
    ''' 基准中的一个运行档位（后端 / 精度 / 是否保留逐步轨迹）。
    ''' </summary>
    Public Class GpuBenchmarkMode

        ''' <summary>档位名称（写入报告与 csv）。</summary>
        Public Property Name As String

        ''' <summary>是否使用 CUDA 后端。</summary>
        Public Property UseGpu As Boolean

        ''' <summary>设备常驻状态的精度档位。</summary>
        Public Property Precision As LifResidentPrecision = LifResidentPrecision.Double64

        ''' <summary>是否保留逐步脉冲轨迹（False = 整段仿真零逐步回读）。</summary>
        Public Property KeepHistory As Boolean = True

        ''' <summary>该档位在数值上是否应当与 CPU 基准<b>逐位一致</b>。</summary>
        ''' <remarks>
        ''' 单精度常驻档会引入膜电位舍入，可能在阈值边界上改变个别脉冲，
        ''' 因此它只作为"最快档"如实报告，不参与硬断言。
        ''' </remarks>
        Public Property ExpectBitExact As Boolean = True

        Public Overrides Function ToString() As String
            Return $"{Name} (gpu={UseGpu}, precision={Precision}, keepHistory={KeepHistory})"
        End Function

    End Class

    ''' <summary>单个档位的运行记录。</summary>
    Public Class GpuBenchmarkEntry

        Public Property Mode As GpuBenchmarkMode
        Public Property Backend As String
        Public Property StepPath As String
        Public Property FallbackSteps As Integer

        ''' <summary>仿真步循环耗时 (毫秒)，<b>不含</b>网络装配与常驻缓冲分配。</summary>
        Public Property ElapsedMs As Long

        ''' <summary>整个档位的墙钟耗时 (毫秒)，含装配 / 常驻分配 / 释放。</summary>
        Public Property WallMs As Long

        Public Property PerStepMs As Double
        Public Property Speedup As Double
        Public Property TotalSpikes As Double
        Public Property ActiveNeurons As Integer
        Public Property Units As Integer
        Public Property PinnedBytes As Long

        ''' <summary>与 CPU 基准逐神经元脉冲计数的最大绝对偏差。</summary>
        Public Property MaxAbsDelta As Double

        ''' <summary>与 CPU 基准脉冲计数不同的神经元数量。</summary>
        Public Property DifferingNeurons As Integer

        ''' <summary>额外的诊断说明（跳过原因等）。</summary>
        Public Property Note As String

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

        Public Sub New()
            MaxAbsDelta = 0
            DifferingNeurons = 0
        End Sub

        Public Overrides Function ToString() As String
            If Skipped Then
                Return $"{Mode.Name}: skipped ({Note})"
            End If

            Return $"{Mode.Name}: {ElapsedMs} ms ({PerStepMs:F3} ms/step), speedup={Speedup:F2}x, " &
                $"spikes={TotalSpikes}, active={ActiveNeurons}/{Units}, maxΔ={MaxAbsDelta:E3}, diffNeurons={DifferingNeurons}"
        End Function

    End Class

    ''' <summary>基准的完整结果。</summary>
    Public Class GpuBenchmarkReport

        ''' <summary>CPU 基准档位（其余档位都与它对拍）。</summary>
        Public Property Baseline As GpuBenchmarkEntry

        Public Property Entries As List(Of GpuBenchmarkEntry)

        ''' <summary>落盘的 csv 文件路径。</summary>
        Public Property CsvFile As String

        ''' <summary>参与硬断言的档位是否全部达标（逐位一致 + 加速比门槛）。</summary>
        Public Property Passed As Boolean

        ''' <summary>加速比门槛。</summary>
        Public Property SpeedupGate As Double

        ''' <summary>参与硬断言的 GPU 档位（用于 demo 断言）。</summary>
        Public ReadOnly Property GatedEntries As GpuBenchmarkEntry()
            Get
                If Entries Is Nothing Then Return New GpuBenchmarkEntry() {}

                Return Entries _
                    .Where(Function(e) e.Mode.UseGpu AndAlso e.Mode.ExpectBitExact AndAlso Not e.Skipped) _
                    .ToArray
            End Get
        End Property

        Public Function Describe() As String
            Dim sb As New StringBuilder()

            Call sb.AppendLine("mode                             backend  path    hist  elapsed    ms/step  speedup   maxΔ       diffNeurons")
            Call sb.AppendLine("----------------------------------------------------------------------------------------------------------")

            For Each entry As GpuBenchmarkEntry In Entries
                Call sb.AppendFormat("{0,-32} {1,-8} {2,-7} {3,-5} ",
                                     entry.Mode.Name,
                                     entry.Backend,
                                     entry.StepPath,
                                     entry.Mode.KeepHistory.ToString)

                If entry.Skipped Then
                    Call sb.AppendLine($"skipped: {entry.Note}")
                Else
                    Call sb.AppendFormat("{0,7} ms {1,9:F3} {2,8:F2}x {3,9:E3} {4,11}",
                                         entry.ElapsedMs,
                                         entry.PerStepMs,
                                         entry.Speedup,
                                         entry.MaxAbsDelta,
                                         entry.DifferingNeurons)
                    Call sb.AppendLine()
                End If
            Next

            Call sb.AppendLine()
            Call sb.AppendLine($"speedup gate: {SpeedupGate:F2}x, passed: {Passed}")

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
    '''   <item><b>执行顺序固定为"先 CPU 档、再 GPU 档"</b>：CPU 档若在 GPU 档之后运行，
    '''         层内已钉住的显存缓冲会失去同步通道（切到 CPU 后端后 <c>UnpinDevice</c>
    '''         落到 CPU 实现上就是空操作），顺序不能颠倒；</item>
    '''   <item>每个档位使用<b>独立</b>的 <see cref="BrainNetwork"/>（共享 CSR），
    '''         并在该档位结束时立即释放常驻显存，避免不同精度档位的缓冲互相残留。</item>
    ''' </list>
    ''' </remarks>
    Public Module GpuBenchmark

        ''' <summary>GPU 档位相对 CPU 基准的最低加速比（验收门槛）。</summary>
        Public Const DefaultSpeedupGate As Double = 3.0

        ''' <summary>档位 → 脉冲计数（对拍用；不污染报告的公开契约）。</summary>
        Private ReadOnly _counts As New Dictionary(Of GpuBenchmarkEntry, Double())

        ''' <summary>
        ''' 默认的档位组合：CPU 基准 + GPU 双精度常驻 + GPU 单精度常驻 + GPU 无历史档。
        ''' </summary>
        Public Function DefaultModes() As GpuBenchmarkMode()
            Return {
                New GpuBenchmarkMode With {
                    .Name = "CPU (SIMD, fused)",
                    .UseGpu = False,
                    .Precision = LifResidentPrecision.Double64,
                    .KeepHistory = True
                },
                New GpuBenchmarkMode With {
                    .Name = "GPU (fp64 resident)",
                    .UseGpu = True,
                    .Precision = LifResidentPrecision.Double64,
                    .KeepHistory = True
                },
                New GpuBenchmarkMode With {
                    .Name = "GPU (fp32 resident)",
                    .UseGpu = True,
                    .Precision = LifResidentPrecision.Single32,
                    .KeepHistory = True,
                    .ExpectBitExact = False
                },
                New GpuBenchmarkMode With {
                    .Name = "GPU (fp64, no history)",
                    .UseGpu = True,
                    .Precision = LifResidentPrecision.Double64,
                    .KeepHistory = False
                }
            }
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
            Dim baseline As GpuBenchmarkEntry = Nothing
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

                    If Not mode.UseGpu AndAlso baseline Is Nothing Then
                        baseline = entry
                    End If
                Next
            Finally
                config.UseGpu = savedUseGpu
                config.ResidentPrecision = savedPrecision
                config.KeepHistory = savedHistory
            End Try

            ' 对拍与加速比
            For Each entry As GpuBenchmarkEntry In entries
                If entry.Skipped Then
                    Continue For
                End If
                If baseline Is Nothing OrElse entry Is baseline Then
                    entry.Speedup = 1.0
                    Continue For
                End If

                entry.Speedup = baseline.ElapsedMs / CDbl(entry.ElapsedMs)

                Dim delta = compareCounts(baseline, entry)

                entry.MaxAbsDelta = delta.MaxDiff
                entry.DifferingNeurons = delta.Differing
            Next

            Dim summary As New GpuBenchmarkReport With {
                .Baseline = baseline,
                .Entries = entries,
                .SpeedupGate = DefaultSpeedupGate
            }

            Dim passed As Boolean = baseline IsNot Nothing AndAlso Not baseline.Skipped

            For Each entry As GpuBenchmarkEntry In summary.GatedEntries
                If entry.DifferingNeurons <> 0 OrElse entry.MaxAbsDelta > 1.0E-9 Then
                    passed = False
                End If
                If entry.Speedup < DefaultSpeedupGate Then
                    passed = False
                End If
            Next

            summary.Passed = passed
            summary.CsvFile = writeCsv(config, summary)

            Return summary
        End Function

        ''' <summary>运行单个档位（内部负责切换后端、下发配置与释放常驻缓冲）。</summary>
        Private Function runMode(config As SnnConfig,
                                 matrix As ConnectomeMatrix,
                                 stimulation As Stimulation,
                                 gain As Double,
                                 mode As GpuBenchmarkMode,
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

            Call storeCounts(entry, result.Counts)
            Call report(reporter, $"    {entry}")

            ' 立即归还常驻显存（常驻缓冲不参与 LRU 淘汰，会挤压后续档位）
            If mode.UseGpu Then
                Call layer.ReleaseDeviceBuffers()
                Call report(reporter, $"    released pinned buffers -> {GpuRuntime.PinnedBytesDescription}")
            End If

            wall.Stop()
            entry.WallMs = wall.ElapsedMilliseconds

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

            Call sb.AppendLine("mode,backend,use_gpu,precision,keep_history,step_path,fallback_steps,elapsed_ms,wall_ms,per_step_ms,speedup,total_spikes,active_neurons,units,max_abs_delta,diff_neurons,pinned_bytes,note")

            For Each entry As GpuBenchmarkEntry In report.Entries
                Call sb.Append(quote(entry.Mode.Name)).Append(",")
                Call sb.Append(quote(entry.Backend)).Append(",")
                Call sb.Append(If(entry.Mode.UseGpu, "true", "false")).Append(",")
                Call sb.Append(entry.Mode.Precision.ToString).Append(",")
                Call sb.Append(If(entry.Mode.KeepHistory, "true", "false")).Append(",")
                Call sb.Append(quote(entry.StepPath)).Append(",")
                Call sb.Append(entry.FallbackSteps).Append(",")
                Call sb.Append(entry.ElapsedMs).Append(",")
                Call sb.Append(entry.WallMs).Append(",")
                Call sb.Append(entry.PerStepMs.ToString("F4")).Append(",")
                Call sb.Append(entry.Speedup.ToString("F4")).Append(",")
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
            Call sb.AppendLine($"{quote("time_steps")},{config.TimeSteps}")
            Call sb.AppendLine($"{quote("global_gain")},{config.GlobalGain}")
            Call sb.AppendLine($"{quote("seed")},{config.Seed}")
            Call sb.AppendLine($"{quote("backend")},{GpuRuntime.BackendName}")
            Call sb.AppendLine($"{quote("units")},{If(report.Baseline Is Nothing, 0, report.Baseline.Units)}")

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
