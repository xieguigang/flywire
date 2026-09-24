Imports System.Globalization
Imports System.IO
Imports System.Text
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork

Namespace Connectome

    ''' <summary>
    ''' 仿真报告：控制台摘要 + 结果 csv 落盘。
    ''' 
    ''' 输出的文件：
    ''' 
    ''' * ``simulation_summary.csv``：配置与本次运行的全局指标 (kv 形式)；
    ''' * ``top_neurons_&lt;mode&gt;.csv``：放电最多的神经元 (附 name / group / class / primary_type 注释)；
    ''' * ``neuron_activity_&lt;mode&gt;.csv``：<b>全量</b>逐神经元脉冲计数 (按索引排列，
    '''   三维可视化据此以仿真活跃度给点云着色 —— 只落盘 top-N 是无法还原全脑热力分布的)；
    ''' * ``group_activity_&lt;mode&gt;.csv``：按 ``names.csv`` 的 group 聚合的活动统计；
    ''' * ``celltype_activity_&lt;mode&gt;.csv``：按 ``consolidated_cell_types.csv`` 的 primary_type 聚合的活动统计；
    ''' * ``per_step_activity_&lt;mode&gt;.csv``：每个时间步的活跃神经元数与脉冲数；
    ''' * ``stimulated_neurons_&lt;mode&gt;.csv``：被外部刺激的神经元清单 (便于复现实验)。
    ''' </summary>
    Public Class SimulationReport

        ''' <summary>结果输出目录。</summary>
        Public ReadOnly Property OutputDir As String

        ''' <summary>本次报告所写入的文件清单。</summary>
        Public ReadOnly Property Files As New List(Of String)()

        Public Sub New(outputDir As String)
            Me.OutputDir = outputDir

            If Not Directory.Exists(outputDir) Then
                Call Directory.CreateDirectory(outputDir)
            End If
        End Sub

        ''' <summary>
        ''' 写入全部报告文件，返回所写入的文件路径数组。
        ''' </summary>
        Public Function Write(result As BrainSimulationResult,
                              config As SnnConfig,
                              network As BrainNetwork,
                              index As ConnectomeIndex,
                              stimulation As Stimulation) As String()

            Dim mode As String = modeName(result.Mode)
            Dim stat As WeightStatistics = network.Statistics()

            Call writeSummary(result, config, network, stat, stimulation, mode)
            Call writeTopNeurons(result, config, index, mode)
            Call writeNeuronCounts(result, index, mode)
            Call writeGroupActivity(result, index, mode)
            Call writeCellTypeActivity(result, index, mode)
            Call writePerStepActivity(result, mode)
            Call writeStimulatedNeurons(stimulation, mode)

            Return Files.ToArray
        End Function

        ''' <summary>
        ''' 控制台摘要。
        ''' </summary>
        Public Sub PrintConsole(result As BrainSimulationResult,
                                config As SnnConfig,
                                network As BrainNetwork,
                                index As ConnectomeIndex,
                                stimulation As Stimulation)

            Dim stat As WeightStatistics = network.Statistics()

            Console.WriteLine()
            Console.WriteLine("    ---- network ----")
            Console.WriteLine($"    neurons (N)         : {network.Units}")
            Console.WriteLine($"    merged edges (nnz)  : {network.Nnz} (csv rows {network.Triplets.CsvRows})")
            Console.WriteLine($"    synapse count total : {network.Triplets.SynapseTotal}")
            Console.WriteLine($"    polarity rows       : E={network.Triplets.ExcitatoryCount}, I={network.Triplets.InhibitoryCount}, no-nt={network.Triplets.EmptyNtTypeCount}")
            Console.WriteLine($"    structural fan-in   : max={network.Triplets.MaxRawFanIn}, mean={network.Triplets.MeanRawFanIn:F1}")
            Console.WriteLine($"    weights after gain  : {stat}")
            Console.WriteLine($"    gain                : {result.Gain}")
            Console.WriteLine($"    stimulation         : {stimulation}")

            Console.WriteLine("    ---- activity ----")
            Console.WriteLine($"    total spikes        : {result.TotalSpikes}")
            Console.WriteLine($"    active neurons      : {result.ActiveNeurons.Length} / {result.Units} ({result.ActiveFraction:P2})")
            Console.WriteLine($"    mean firing rate    : {result.MeanFiringRate:E3} / neuron / step")
            Console.WriteLine($"    active neuron rate  : {result.MeanActiveRate:F4} / step")
            Console.WriteLine($"    elapsed             : {result.ElapsedMs} ms ({result.TimeSteps} steps)")

            Call printTop(result, index, 10)
            Call printAggregate("group", groupActivity(result, index), 8)
            Call printAggregate("primary type", cellTypeActivity(result, index), 8)

            Console.WriteLine("    ---- per step ----")

            For t As Integer = 0 To result.PerStepActive.Length - 1
                Console.WriteLine($"    step {t + 1,3}: active={result.PerStepActive(t),6}, spikes={result.PerStepSpikes(t),10}")
            Next
        End Sub

#Region "console helpers"

        Private Sub printTop(result As BrainSimulationResult, index As ConnectomeIndex, top As Integer)
            Console.WriteLine($"    ---- top {top} neurons ----")
            Console.WriteLine("    rank  root_id                spikes  name                      group                 primary_type")

            For Each item In topNeurons(result, index, top)
                Console.WriteLine(
                    $"    {item.Rank,4}  {item.RootId,-20}  {item.Spikes,6}  {item.Name,-24}  {item.Group,-20}  {item.PrimaryType}")
            Next
        End Sub

        Private Sub printAggregate(name As String, rows As IEnumerable(Of ActivityRow), top As Integer)
            Console.WriteLine($"    ---- top {top} {name} ----")

            For Each row In rows.Take(top)
                Console.WriteLine($"    {row.Key,-24} neurons={row.Neurons,6}, active={row.Active,6}, spikes={row.Spikes,10}, rate={row.MeanRate:F4}")
            Next
        End Sub

#End Region

#Region "data aggregation"

        ''' <summary>聚合之后的活动行。</summary>
        Public Class ActivityRow
            Public Property Key As String = ""
            Public Property Neurons As Integer
            Public Property Active As Integer
            Public Property Spikes As Double

            Public ReadOnly Property MeanRate As Double
                Get
                    If Neurons = 0 Then Return 0
                    Return Spikes / Neurons
                End Get
            End Property
        End Class

        Public Class TopNeuronRow
            Public Property Rank As Integer
            Public Property Index As Integer
            Public Property RootId As Long
            Public Property Spikes As Double
            Public Property Name As String = ""
            Public Property Group As String = ""
            Public Property ClassName As String = ""
            Public Property PrimaryType As String = ""
            Public Property Excitatory As Boolean = True
        End Class

        ''' <summary>按注释字段聚合活动 (空注释归入 "(unannotated)")。</summary>
        Public Shared Function aggregate(result As BrainSimulationResult,
                                         index As ConnectomeIndex,
                                         selector As Func(Of Integer, String)) As ActivityRow()

            Dim groups As New Dictionary(Of String, ActivityRow)()

            For i As Integer = 0 To result.Units - 1
                Dim key As String = If(selector(i), "")

                If key.Length = 0 Then
                    key = "(unannotated)"
                End If

                Dim row As ActivityRow = Nothing

                If Not groups.TryGetValue(key, row) Then
                    row = New ActivityRow With {.Key = key}
                    Call groups.Add(key, row)
                End If

                row.Neurons += 1
                row.Spikes += result.Counts(i)

                If result.Counts(i) > 0 Then
                    row.Active += 1
                End If
            Next

            Return groups.Values.OrderByDescending(Function(r) r.Spikes).ThenBy(Function(r) r.Key).ToArray
        End Function

        Public Shared Function groupActivity(result As BrainSimulationResult, index As ConnectomeIndex) As ActivityRow()
            Return aggregate(result, index, Function(i As Integer) index.GetGroup(i))
        End Function

        Public Shared Function cellTypeActivity(result As BrainSimulationResult, index As ConnectomeIndex) As ActivityRow()
            Return aggregate(result, index, Function(i As Integer) index.GetPrimaryType(i))
        End Function

        Public Shared Function topNeurons(result As BrainSimulationResult, index As ConnectomeIndex, count As Integer) As TopNeuronRow()
            Dim rows As New List(Of TopNeuronRow)()
            Dim ordered As Integer() = Enumerable _
                .Range(0, result.Units) _
                .Where(Function(i) result.Counts(i) > 0) _
                .OrderByDescending(Function(i) result.Counts(i)) _
                .ThenBy(Function(i) i) _
                .Take(count) _
                .ToArray

            Dim rank As Integer = 0

            For Each i As Integer In ordered
                rank += 1

                Call rows.Add(New TopNeuronRow With {
                    .Rank = rank,
                    .Index = i,
                    .RootId = index.GetRootId(i),
                    .Spikes = result.Counts(i),
                    .Name = index.GetName(i),
                    .Group = index.GetGroup(i),
                    .ClassName = index.GetClass(i),
                    .PrimaryType = index.GetPrimaryType(i),
                    .Excitatory = index.IsExcitatory(i)
                })
            Next

            Return rows.ToArray
        End Function

#End Region

#Region "csv writers"

        Private Sub writeSummary(result As BrainSimulationResult,
                                 config As SnnConfig,
                                 network As BrainNetwork,
                                 stat As WeightStatistics,
                                 stimulation As Stimulation,
                                 mode As String)

            Dim sb As New StringBuilder()

            Call sb.AppendLine("key,value")
            Call sb.AppendLine(csv("generated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")))
            Call sb.AppendLine(csv("stimulation_mode", result.Mode.ToString))
            Call sb.AppendLine(csv("stimulation_label", stimulation.Label))
            Call sb.AppendLine(csv("stimulated_neurons", stimulation.Count))

            For Each kv As KeyValuePair(Of String, String) In config.ToKeyValues()
                ' 运行模式已经在上面写入 (含标定之后的实际取值)，这里跳过配置之中的同名项
                If kv.Key = "stimulation_mode" Then
                    Continue For
                End If

                Call sb.AppendLine(csv(kv.Key, kv.Value))
            Next

            Call sb.AppendLine(csv("neurons", network.Units))
            Call sb.AppendLine(csv("csv_rows", network.Triplets.CsvRows))
            Call sb.AppendLine(csv("nnz", network.Nnz))
            Call sb.AppendLine(csv("synapse_total", network.Triplets.SynapseTotal))
            Call sb.AppendLine(csv("excitatory_rows", network.Triplets.ExcitatoryCount))
            Call sb.AppendLine(csv("inhibitory_rows", network.Triplets.InhibitoryCount))
            Call sb.AppendLine(csv("empty_nt_rows", network.Triplets.EmptyNtTypeCount))
            Call sb.AppendLine(csv("weight_min", stat.Min))
            Call sb.AppendLine(csv("weight_max", stat.Max))
            Call sb.AppendLine(csv("weight_mean_abs", stat.MeanAbs))
            Call sb.AppendLine(csv("weight_nan", stat.NaNCount))
            Call sb.AppendLine(csv("weight_inf", stat.InfinityCount))
            Call sb.AppendLine(csv("calibrated_gain", result.Gain))
            Call sb.AppendLine(csv("time_steps_used", result.TimeSteps))
            Call sb.AppendLine(csv("total_spikes", result.TotalSpikes))
            Call sb.AppendLine(csv("active_neurons", result.ActiveNeurons.Length))
            Call sb.AppendLine(csv("active_fraction", result.ActiveFraction))
            Call sb.AppendLine(csv("mean_firing_rate", result.MeanFiringRate))
            Call sb.AppendLine(csv("mean_active_rate", result.MeanActiveRate))
            Call sb.AppendLine(csv("elapsed_ms", result.ElapsedMs))
            Call sb.AppendLine(csv("snn_library_total_spikes",
                                  SpikeDecoders.TotalSpikeCount(network.Network.SparseLayer.SHistory)))

            Call writeFile($"simulation_summary_{mode}.csv", sb.ToString)
        End Sub

        Private Sub writeTopNeurons(result As BrainSimulationResult, config As SnnConfig, index As ConnectomeIndex, mode As String)
            Dim sb As New StringBuilder()

            Call sb.AppendLine("rank,neuron_index,root_id,spike_count,firing_rate,name,group,class,primary_type,excitatory")

            For Each row In topNeurons(result, index, config.TopNeurons)
                Call sb.AppendLine(String.Join(",",
                    row.Rank,
                    row.Index,
                    row.RootId,
                    num(row.Spikes),
                    num(row.Spikes / result.TimeSteps),
                    csv(row.Name),
                    csv(row.Group),
                    csv(row.ClassName),
                    csv(row.PrimaryType),
                    row.Excitatory.ToString))
            Next

            Call writeFile($"top_neurons_{mode}.csv", sb.ToString)
        End Sub

        ''' <summary>
        ''' 全量逐神经元脉冲计数 (按神经元索引排列)。
        ''' </summary>
        ''' <remarks>
        ''' 三维可视化需要<b>每一个</b>神经元的活跃度才能画出全脑的热力分布，
        ''' 而 ``top_neurons`` 只保留了前 N 个，聚合表又丢掉了空间分布。
        ''' 这个文件不大 (139,255 行约 5 MB)，但让"看仿真结果"不再需要重跑仿真。
        ''' </remarks>
        Private Sub writeNeuronCounts(result As BrainSimulationResult, index As ConnectomeIndex, mode As String)
            Dim sb As New StringBuilder()

            Call sb.AppendLine("neuron_index,root_id,spike_count,firing_rate")

            For i As Integer = 0 To result.Units - 1
                Call sb.AppendLine(String.Join(",",
                    i,
                    index.GetRootId(i),
                    num(result.Counts(i)),
                    num(result.Counts(i) / result.TimeSteps)))
            Next

            Call writeFile($"neuron_activity_{mode}.csv", sb.ToString)
        End Sub

        Private Sub writeGroupActivity(result As BrainSimulationResult, index As ConnectomeIndex, mode As String)
            Call writeActivity($"group_activity_{mode}.csv", groupActivity(result, index))
        End Sub

        Private Sub writeCellTypeActivity(result As BrainSimulationResult, index As ConnectomeIndex, mode As String)
            Call writeActivity($"celltype_activity_{mode}.csv", cellTypeActivity(result, index))
        End Sub

        Private Sub writeActivity(fileName As String, rows As ActivityRow())
            Dim sb As New StringBuilder()

            Call sb.AppendLine("key,neurons,active_neurons,total_spikes,mean_spikes_per_neuron,active_fraction")

            For Each row In rows
                Call sb.AppendLine(String.Join(",",
                    csv(row.Key),
                    row.Neurons,
                    row.Active,
                    num(row.Spikes),
                    num(row.MeanRate),
                    num(If(row.Neurons = 0, 0, row.Active / CDbl(row.Neurons)))))
            Next

            Call writeFile(fileName, sb.ToString)
        End Sub

        Private Sub writePerStepActivity(result As BrainSimulationResult, mode As String)
            Dim sb As New StringBuilder()
            Dim total As Double = 0

            Call sb.AppendLine("step,active_neurons,spikes,cumulative_spikes")

            For t As Integer = 0 To result.PerStepActive.Length - 1
                total += result.PerStepSpikes(t)

                Call sb.AppendLine(String.Join(",",
                    t + 1,
                    result.PerStepActive(t),
                    num(result.PerStepSpikes(t)),
                    num(total)))
            Next

            Call writeFile($"per_step_activity_{mode}.csv", sb.ToString)
        End Sub

        Private Sub writeStimulatedNeurons(stimulation As Stimulation, mode As String)
            Dim sb As New StringBuilder()

            Call sb.AppendLine("feature_index,neuron_index,root_id,input_value")

            For i As Integer = 0 To stimulation.Count - 1
                Call sb.AppendLine(String.Join(",",
                    i,
                    stimulation.InputMap(i),
                    stimulation.RootIds(i),
                    num(stimulation.Values(i))))
            Next

            Call writeFile($"stimulated_neurons_{mode}.csv", sb.ToString)
        End Sub

        Private Sub writeFile(fileName As String, content As String)
            Dim fullPath As String = System.IO.Path.Combine(OutputDir, fileName)

            Call System.IO.File.WriteAllText(fullPath, content, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
            Call Files.Add(fullPath)
        End Sub

#End Region

        ''' <summary>模式名称 (用于结果文件的文件名后缀)。</summary>
        Public Shared Function modeName(mode As StimulationMode) As String
            If mode = StimulationMode.CellType Then
                Return "celltype"
            Else
                Return "random"
            End If
        End Function

        Private Shared Function num(value As Double) As String
            Return value.ToString("G9", CultureInfo.InvariantCulture)
        End Function

        ''' <summary>输出一行 ``key,value`` (用于 summary 的 kv 形式)。</summary>
        Private Shared Function csv(key As String, value As Object) As String
            Return csv(key) & "," & csv(If(value Is Nothing, "", value.ToString))
        End Function

        ''' <summary>csv 字段转义 (含逗号 / 引号 / 换行的字段加引号)。</summary>
        Private Shared Function csv(value As String) As String
            Dim text As String = If(value, "")

            If text.IndexOfAny(New Char() {","c, """"c, ControlChars.Cr, ControlChars.Lf}) >= 0 Then
                Return """" & text.Replace("""", """") & """"
            Else
                Return text
            End If
        End Function

    End Class
End Namespace
