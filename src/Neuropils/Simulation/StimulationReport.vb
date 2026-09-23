Imports System.Globalization
Imports System.IO
Imports System.Text

Namespace Simulation

    ''' <summary>
    ''' 把一次电刺激仿真的结果落盘（"记录仿真结果"）。
    ''' </summary>
    ''' <remarks>
    ''' 输出四份 csv 到 ``&lt;DataDir&gt;\snn-output\&lt;时间戳&gt;_stimulation\``：
    ''' 
    ''' * ``stimulation_summary.csv``：刺激与仿真的元信息（kv 形式，便于拼进批量实验的汇总表）；
    ''' * ``stimulation_steps.csv``：逐步统计（步号、活跃数、脉冲数、累计）；
    ''' * ``stimulation_active_neurons.csv``：<b>逐步激活神经元清单</b>（回放数据源，可离线复现回放）；
    ''' * ``neuron_activity_stimulation.csv``：逐神经元累计脉冲计数 ——
    '''   与 <c>SimulationReport</c> 的同名表同构，因此可视化端的"仿真活跃度"着色
    '''   可以直接把本次刺激的结果当作一个活跃度快照使用（重新载入数据即可选中）。
    ''' 
    ''' 写成"每次刺激一个目录"而不是覆盖同名文件：电刺激实验往往是"同一点位扫强度"
    ''' 或"同强度扫点位"，逐次覆盖会让对照关系无从追溯。
    ''' </remarks>
    Public NotInheritable Class StimulationReport

        ''' <summary>写入一次刺激的全部报告文件，返回落盘目录。</summary>
        Public Shared Function Write(replay As StimulationReplay,
                                     outputRoot As String,
                                     Optional index As FlywireAI.Connectome.ConnectomeIndex = Nothing) As String

            If replay Is Nothing Then Throw New ArgumentNullException(NameOf(replay))

            Dim dir As String = Path.Combine(outputRoot, $"{DateTime.Now:yyyyMMdd_HHmmss}_stimulation")

            If Not Directory.Exists(dir) Then
                Call Directory.CreateDirectory(dir)
            End If

            Dim files As New List(Of String)()

            Call files.Add(writeText(Path.Combine(dir, "stimulation_summary.csv"), summary(replay)))
            Call files.Add(writeText(Path.Combine(dir, "stimulation_steps.csv"), steps(replay)))
            Call files.Add(writeText(Path.Combine(dir, "stimulation_active_neurons.csv"), activeNeurons(replay, index)))
            Call files.Add(writeText(Path.Combine(dir, "neuron_activity_stimulation.csv"), neuronActivity(replay, index)))

            ' 响应曲线数据：只有真的拿到了膜电位才写（没有分析回放时曲线图会用脉冲率代替）
            If replay.ResponsePotential.Length > 0 Then
                Call files.Add(writeText(Path.Combine(dir, "stimulation_response_potential.csv"),
                                         responsePotential(replay, index)))
            End If

            replay.ReportDir = dir
            replay.ReportFiles = files.ToArray()

            Return dir
        End Function

        ''' <remarks>
        ''' 形参不能叫 ``file``：会遮蔽 <see cref="System.IO.File"/>（VB 不区分大小写），
        ''' 于是 <c>File.WriteAllText</c> 会被解析成"字符串上的成员"而编译失败。
        ''' </remarks>
        Private Shared Function writeText(path As String, content As String) As String
            Call IO.File.WriteAllText(path, content, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))

            Return path
        End Function

        Private Shared Function summary(replay As StimulationReplay) As String
            Dim sb As New StringBuilder()

            Call sb.AppendLine("key,value")
            Call sb.AppendLine(csv("generated_at", replay.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss")))
            Call sb.AppendLine(csv("neuron_index", replay.Neuron))
            Call sb.AppendLine(csv("root_id", replay.RootId))
            Call sb.AppendLine(csv("strength", num(replay.Strength)))
            Call sb.AppendLine(csv("recruited_neurons", replay.RecruitedNeurons))
            Call sb.AppendLine(csv("radius_um", num(replay.RadiusNm / 1000.0)))
            Call sb.AppendLine(csv("hold_ms", replay.HoldMilliseconds))
            Call sb.AppendLine(csv("time_steps", replay.Steps))
            Call sb.AppendLine(csv("neurons", replay.Units))
            Call sb.AppendLine(csv("gain", num(replay.Gain)))
            Call sb.AppendLine(csv("backend", replay.Backend))
            Call sb.AppendLine(csv("step_path", replay.StepPath))
            Call sb.AppendLine(csv("fallback_steps", replay.FallbackSteps))
            Call sb.AppendLine(csv("elapsed_ms", replay.ElapsedMs))
            Call sb.AppendLine(csv("wall_ms", replay.WallMs))
            Call sb.AppendLine(csv("total_spikes", num(replay.TotalSpikes)))
            Call sb.AppendLine(csv("active_neurons", replay.ActiveUnionCount))
            Call sb.AppendLine(csv("active_fraction", num(replay.ActiveFraction)))
            Call sb.AppendLine(csv("peak_step", replay.PeakStep + 1))
            Call sb.AppendLine(csv("peak_active", replay.PeakActive))
            Call sb.AppendLine(csv("response_signal", replay.ResponseSignal))
            Call sb.AppendLine(csv("response_neurons", replay.ResponseNeurons.Length))
            Call sb.AppendLine(csv("analysis_ms", replay.AnalysisMs))

            If replay.AnalysisMatches.HasValue Then
                Call sb.AppendLine(csv("analysis_matches", replay.AnalysisMatches.Value))
                Call sb.AppendLine(csv("analysis_mismatches", replay.AnalysisMismatches))
            End If

            Return sb.ToString
        End Function

        ''' <summary>
        ''' 逐神经元逐步的响应电信号矩阵（曲线图的数据源）。
        ''' </summary>
        ''' <remarks>
        ''' 一行一个"有响应"的神经元，一列一个时间步（<c>t1..tT</c>）。
        ''' 之所以把时间步铺成列而不是"每个神经元-步一行"：前者是一张标准的
        ''' 神经元 × 时间矩阵，行数只有几千、列数只有几十，读回来可以直接画曲线；
        ''' 后者会膨胀成几十万行，纯粹是解析开销。
        ''' </remarks>
        Private Shared Function responsePotential(replay As StimulationReplay,
                                                 index As FlywireAI.Connectome.ConnectomeIndex) As String
            Dim sb As New StringBuilder()
            Dim header As New List(Of String) From {"neuron_index", "root_id"}

            For t As Integer = 1 To replay.Steps
                Call header.Add($"t{t}")
            Next

            Call sb.AppendLine(String.Join(",", header))

            For k As Integer = 0 To replay.ResponseNeurons.Length - 1
                Dim neuron As Integer = replay.ResponseNeurons(k)
                Dim row As Double() = replay.ResponsePotential(k)
                Dim cells As New List(Of String) From {
                    neuron.ToString,
                    If(index Is Nothing, "", index.GetRootId(neuron).ToString)
                }

                For t As Integer = 0 To replay.Steps - 1
                    Call cells.Add(num(If(row IsNot Nothing AndAlso t < row.Length, row(t), 0)))
                Next

                Call sb.AppendLine(String.Join(",", cells))
            Next

            Return sb.ToString
        End Function

        Private Shared Function steps(replay As StimulationReplay) As String
            Dim sb As New StringBuilder()
            Dim cumulative As Double = 0

            Call sb.AppendLine("step,active_neurons,spikes,cumulative_spikes")

            For t As Integer = 0 To replay.Steps - 1
                Dim spikes As Double = If(replay.SpikesPerStep IsNot Nothing AndAlso t < replay.SpikesPerStep.Length,
                                          replay.SpikesPerStep(t), 0)

                cumulative += spikes

                Call sb.AppendLine(String.Join(",",
                                               t + 1,
                                               If(replay.ActivePerStep IsNot Nothing AndAlso t < replay.ActivePerStep.Length,
                                                  replay.ActivePerStep(t), 0),
                                               num(spikes),
                                               num(cumulative)))
            Next

            Return sb.ToString
        End Function

        ''' <summary>逐步激活神经元清单（回放数据源）。</summary>
        Private Shared Function activeNeurons(replay As StimulationReplay,
                                              index As FlywireAI.Connectome.ConnectomeIndex) As String
            Dim sb As New StringBuilder()

            Call sb.AppendLine("step,neuron_index,root_id")

            For t As Integer = 0 To replay.Steps - 1
                Dim active As Integer() = replay.StepActive(t)

                For i As Integer = 0 To active.Length - 1
                    Dim neuron As Integer = active(i)

                    Call sb.AppendLine(String.Join(",",
                                                   t + 1,
                                                   neuron,
                                                   If(index Is Nothing, "", index.GetRootId(neuron).ToString)))
                Next
            Next

            Return sb.ToString
        End Function

        ''' <summary>逐神经元累计计数（与 SimulationReport 的同名表同构）。</summary>
        Private Shared Function neuronActivity(replay As StimulationReplay,
                                               index As FlywireAI.Connectome.ConnectomeIndex) As String
            Dim sb As New StringBuilder()

            Call sb.AppendLine("neuron_index,root_id,spike_count,firing_rate")

            For i As Integer = 0 To replay.Units - 1
                Dim spikes As Double = If(replay.Counts IsNot Nothing AndAlso i < replay.Counts.Length, replay.Counts(i), 0)

                Call sb.AppendLine(String.Join(",",
                                               i,
                                               If(index Is Nothing, "", index.GetRootId(i).ToString),
                                               num(spikes),
                                               num(spikes / System.Math.Max(1, replay.Steps))))
            Next

            Return sb.ToString
        End Function

        Private Shared Function num(value As Double) As String
            Return value.ToString("G9", CultureInfo.InvariantCulture)
        End Function

        ''' <summary>``key,value`` 形式的一行（value 里的逗号/引号会被转义）。</summary>
        Private Shared Function csv(key As String, value As Object) As String
            Dim text As String = If(value Is Nothing, "", Convert.ToString(value, CultureInfo.InvariantCulture))

            Return key & "," & escape(text)
        End Function

        Private Shared Function escape(value As String) As String
            If value Is Nothing Then Return ""

            If value.IndexOf(","c) < 0 AndAlso value.IndexOf(""""c) < 0 Then Return value

            Return """" & value.Replace("""", """""") & """"
        End Function

    End Class

End Namespace
