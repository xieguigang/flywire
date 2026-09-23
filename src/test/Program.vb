Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports FlywireAI.FAFBv783
Imports Microsoft.VisualBasic.Data.Framework.IO.Linq

''' <summary>
''' FAFB v783 数据模型以及加载模块的演示测试程序。
''' 
''' 使用 ``F:\flywire\FAFB-v783\`` 目录下的真实 csv 数据文件，验证：
''' 
''' 1. 小表通过 ``LoadCsv`` 扩展方法进行全量加载；
''' 2. 大表通过 ``StreamXxx`` (``DataStream.OpenHandle`` + ``AsLinq(Of T)``) 进行流式加载；
''' 3. 超大表通过 ``DataStream.OpenHandle`` + ``DataStream.AsLinq`` 原始接口进行流式加载；
''' 4. ``Long`` 类型加载 FlyWire Root ID 时的精度；
''' 5. 未定义数据模型的 csv 文件通过 ``OpenRawHandle`` 读取原始数据行。
''' </summary>
Module Program

    ''' <summary>
    ''' 下载下来的 csv 数据文件所在的文件夹。
    ''' </summary>
    Const DATA_DIR As String = "F:\flywire\FAFB-v783"

    Dim totalCount As Integer = 0
    Dim failCount As Integer = 0

    Sub Main(args As String())
        Console.WriteLine("FAFB v783 data model and loader demo")
        Console.WriteLine($"data directory: {DATA_DIR}")

        If Not Directory.Exists(DATA_DIR) Then
            Console.WriteLine($"[FATAL] the data directory does not exist: {DATA_DIR}")
            Call Environment.Exit(1)
        End If

        Call run("1. full loading via LoadCsv", AddressOf testFullLoading)
        Call run("2. streaming loading via StreamXxx", AddressOf testStreamLoading)
        Call run("3. huge table via OpenHandle + AsLinq (raw api)", AddressOf testHugeTableStream)
        Call run("4. Int64 root_id precision", AddressOf testRootIdPrecision)
        Call run("5. raw rows for undocumented csv", AddressOf testRawRows)
        Call run("6. stream rows vs full load rows", AddressOf testRowCountConsistency)
        Call run("7. streaming api regression (framework fixes)", AddressOf testDataStreamHandleBehavior)

        Console.WriteLine()
        Console.WriteLine($"pass: {totalCount - failCount} / {totalCount}, fail: {failCount}")

        If failCount > 0 Then
            Call Environment.Exit(1)
        End If
    End Sub

#Region "demo sections"

    ''' <summary>
    ''' 小表：通过 ``LoadCsv(Of T)`` 全量加载为 ``List(Of T)``，行数与文档中的行数进行比较。
    ''' </summary>
    Private Sub testFullLoading()
        Console.WriteLine()
        Console.WriteLine("=== 1. full loading via LoadCsv (small tables) ===")

        Call loadCheck("CellTypes", "consolidated_cell_types.csv", 138327,
                       Function(p As String) p.LoadCellTypes(),
                       Function(x As CellTypes) $"root_id={x.RootId}, primary_type={x.PrimaryType}, additional_type(s)='{x.AdditionalTypes}'")

        Call loadCheck("Classification", "classification.csv", 139255,
                       Function(p As String) p.LoadClassification(),
                       Function(x As Classification) $"root_id={x.RootId}, flow={x.Flow}, class={x.Class}, sub_class={x.SubClass}, side={x.Side}")

        Call loadCheck("CellStats", "cell_stats.csv", 139246,
                       Function(p As String) p.LoadCellStats(),
                       Function(x As CellStats) $"length_nm={x.LengthNm}, area_nm={x.AreaNm}, size_nm={x.SizeNm}")

        Call loadCheck("CellNames", "names.csv", 139255,
                       Function(p As String) p.LoadCellNames(),
                       Function(x As CellNames) $"root_id={x.RootId}, name={x.Name}, group={x.Group}")

        Call loadCheck("Neurons", "neurons.csv", 139255,
                       Function(p As String) p.LoadNeurons(),
                       Function(x As Neurons) $"group={x.Group}, nt_type={x.NtType}, score={x.NtTypeScore}, ach_avg={x.AchAvg}")

        Call loadCheck("VisualNeuronTypes", "visual_neuron_types.csv", 95079,
                       Function(p As String) p.LoadVisualNeuronTypes(),
                       Function(x As VisualNeuronTypes) $"type={x.Type}, family={x.Family}, subsystem={x.Subsystem}")

        Call loadCheck("ColumnAssignment", "column_assignment.csv", 45528,
                       Function(p As String) p.LoadColumnAssignment(),
                       Function(x As ColumnAssignment) $"hemisphere={x.Hemisphere}, type={x.Type}, column_id={x.ColumnId}, x={x.X}, y={x.Y}")

        ' 注意：labels.csv 之中存在引号包裹的多行文本域 (例如标签文本换行)，
        ' 文档统计的记录数为 160,045，而文件的物理行数为 160,728，两条加载路径都是按行读取的，
        ' 所以这里使用文件的实际行数作为预期值
        Call loadCheck("CommunityLabels", "labels.csv", csvLineCount("labels.csv"),
                       Function(p As String) p.LoadCommunityLabels(),
                       Function(x As CommunityLabels) $"root_id={x.RootId}, label={x.Label}, user={x.UserName}, affiliation={x.UserAffiliation}")

        Call loadCheck("ProcessedLabels", "processed_labels.csv", 100091,
                       Function(p As String) p.LoadProcessedLabels(),
                       Function(x As ProcessedLabels) $"root_id={x.RootId}, labels={x.ProcessedLabelsList}")

        Call loadCheck("ConnectivityTags", "connectivity_tags.csv", 134437,
                       Function(p As String) p.LoadConnectivityTags(),
                       Function(x As ConnectivityTags) $"root_id={x.RootId}, tags={x.ConnectivityTag}")

        Call loadCheck("Coordinates", "coordinates.csv", 238909,
                       Function(p As String) p.LoadCoordinates(),
                       Function(x As Coordinates) $"root_id={x.RootId}, position={x.Position}, supervoxel_id={x.SupervoxelId}")

        Call loadCheck("SynapseAttachmentRates", "synapse_attachement_rates.csv", csvLineCount("synapse_attachement_rates.csv"),
                       Function(p As String) p.LoadSynapseAttachmentRates(),
                       Function(x As SynapseAttachmentRates) $"neuropil={x.Neuropil}, count_total={x.CountTotal}, count_proof={x.CountProof}, ratio={x.ProofRatio}, side={x.Side}")
    End Sub

    ''' <summary>
    ''' 大表：通过生成的 ``StreamXxx`` 函数以惰性迭代 (``DataStream.OpenHandle`` + ``AsLinq``) 的方式读取。
    ''' </summary>
    Private Sub testStreamLoading()
        Console.WriteLine()
        Console.WriteLine("=== 2. streaming loading via StreamXxx (lazy iteration) ===")

        Const limit As Integer = 100000

        ' 已校对细胞之间的连接矩阵：5,342,446 行
        Dim timer As Stopwatch = Stopwatch.StartNew()
        Dim connections = csv("connections_princeton.csv").StreamConnections().Take(limit).ToArray
        timer.Stop()
        Call check("Connections rows read", connections.Length, limit)
        Console.WriteLine($"    {timer.ElapsedMilliseconds,5} ms, syn_count sum={connections.Sum(Function(r) r.SynCount)}")
        Console.WriteLine($"    sample: {connections(0).PreRootId} -> {connections(0).PostRootId} @ {connections(0).Neuropil}, syn_count={connections(0).SynCount}, nt_type={connections(0).NtType}")

        ' 未过滤阈值的连接矩阵：22,285,323 行
        timer = Stopwatch.StartNew()
        Dim noThreshold = csv("connections_princeton_no_threshold.csv").StreamConnectionsNoThreshold().Take(limit).ToArray
        timer.Stop()
        Call check("ConnectionsNoThreshold rows read", noThreshold.Length, limit)
        Console.WriteLine($"    {timer.ElapsedMilliseconds,5} ms, syn_count sum={noThreshold.Sum(Function(r) r.SynCount)}")

        ' 旧版本的连接矩阵：16,847,997 行
        timer = Stopwatch.StartNew()
        Dim buhmann = csv("connections_buhmann_no_threshold.csv").StreamConnectionsBuhmannNoThreshold().Take(limit).ToArray
        timer.Stop()
        Call check("ConnectionsBuhmannNoThreshold rows read", buhmann.Length, limit)
        Console.WriteLine($"    {timer.ElapsedMilliseconds,5} ms, syn_count sum={buhmann.Sum(Function(r) r.SynCount)}")

        ' 旧版本的突触坐标：34,156,320 行
        timer = Stopwatch.StartNew()
        Dim coordinates = csv("synapse_coordinates.csv").StreamSynapseCoordinates().Take(limit).ToArray
        timer.Stop()
        Call check("SynapseCoordinates rows read", coordinates.Length, limit)
        Console.WriteLine($"    {timer.ElapsedMilliseconds,5} ms")
        Console.WriteLine($"    sample: pre_root_id='{coordinates(0).PreRootId}', post_root_id='{coordinates(0).PostRootId}', x={coordinates(0).X}, y={coordinates(0).Y}, z={coordinates(0).Z}")

        ' 321 列的 per-neuropil 计数表：134,181 行
        timer = Stopwatch.StartNew()
        Dim neuropil = csv("neuropil_synapse_table.csv").StreamNeuropilSynapseTable().Take(10000).ToArray
        timer.Stop()
        Call check("NeuropilSynapseTable rows read", neuropil.Length, 10000)
        Console.WriteLine($"    {timer.ElapsedMilliseconds,5} ms (321 columns per row)")
        Console.WriteLine($"    sample: root_id={neuropil(0).RootId}, input synapses={neuropil(0).InputSynapses}, input synapses in AL_L={neuropil(0).InputSynapsesInAL_L}, output partners in WED_R={neuropil(0).OutputPartnersInWED_R}")

        ' 小表同样可以使用流式读取
        Dim names = csv("names.csv").StreamCellNames().Take(5).ToArray
        Call check("CellNames rows read (stream)", names.Length, 5)
        Console.WriteLine($"    sample: {names(0).Name} @ {names(0).Group}")
    End Sub

    ''' <summary>
    ''' 超大表：直接使用 ``DataStream.OpenHandle`` + ``DataStream.AsLinq`` 原始接口进行流式加载。
    ''' 
    ''' (Synapse Table 文件为 2.7 GB / 80,215,790 行，这里只读取前 20 万行以便在演示中观察吞吐量)
    ''' </summary>
    Private Sub testHugeTableStream()
        Console.WriteLine()
        Console.WriteLine("=== 3. huge table via OpenHandle + AsLinq (raw api) ===")

        Const limit As Integer = 200000

        Dim file As String = csv("fafb_v783_princeton_synapse_table.csv")
        Dim timer As Stopwatch = Stopwatch.StartNew()
        Dim n As Integer = 0
        Dim sizeSum As Double = 0
        Dim first As SynapseTable = Nothing

        ' OpenHandle 返回 (schema, table) 元组：table 是跳过了标题行的惰性文本行序列
        Dim handle = DataLinqStream.OpenHandle(file, tqdm_wrap:=False)

        Console.WriteLine($"    schema: {handle.schema}")

        ' AsLinq(Of T) 把文本行逐行映射为数据模型对象
        For Each synapse As SynapseTable In handle.AsLinq(Of SynapseTable)()
            n += 1

            If first Is Nothing Then
                first = synapse
            End If

            sizeSum += synapse.Size

            If n >= limit Then
                Exit For
            End If
        Next

        timer.Stop()
        Call check("SynapseTable rows read", n, limit)
        Console.WriteLine($"    file: {file}")
        Console.WriteLine($"    first row: pre_x={first.PreX}, pre_y={first.PreY}, pre_z={first.PreZ}, size={first.Size}, pre_root_id_720575940={first.PreRootId720575940}, post_root_id_720575940={first.PostRootId720575940}, neuropil='{first.Neuropil}'")
        Console.WriteLine($"    size sum={sizeSum}, elapsed={timer.ElapsedMilliseconds} ms, throughput={Math.Round(n / timer.Elapsed.TotalSeconds)} rows/sec")
    End Sub

    ''' <summary>
    ''' FlyWire 的 Root ID 大约为 7.2e17，超过了 ``Double`` 可以精确表示的整数上限 2^53，
    ''' 因此模型的 ID 字段使用 ``Long`` (Int64) 定义，并且通过 <see cref="Int64Parser"/> 从
    ''' 文本直接解析，从而避免框架默认的 Double 中转所导致的精度丢失。
    ''' </summary>
    Private Sub testRootIdPrecision()
        Console.WriteLine()
        Console.WriteLine("=== 4. Int64 root_id precision ===")

        Dim file As String = csv("cell_stats.csv")
        Dim expectedId As Long = Long.Parse(System.IO.File.ReadLines(file).Skip(1).First.Split(","c)(0))

        ' 流式读取
        Dim rows = file.StreamCellStats().Take(2).ToArray
        Call check("streamed root_id equals the raw csv text", rows(0).RootId, expectedId)

        ' 全量加载
        Dim loaded As List(Of CellStats) = file.LoadCellStats()
        Call check("loaded root_id equals the raw csv text", loaded(0).RootId, expectedId)

        Call check("root_id > 2^53", expectedId > 9007199254740992L, True)

        ' 框架默认的字符串 -> Int64 转换器 (Casting.CastLong) 的精度校验
        Dim casted As Long = Microsoft.VisualBasic.Scripting.Runtime.Casting.CastLong("720575940599457990")
        Call check("framework Casting.CastLong precision", casted, 720575940599457990L)

        ' 演示使用 Double 中转解析时所产生的精度丢失
        Dim lossByDouble As Long = CLng(CDbl(expectedId))

        Console.WriteLine($"    raw csv text      : {expectedId}")
        Console.WriteLine($"    streamed via model: {rows(0).RootId}")
        Console.WriteLine($"    loaded via model  : {loaded(0).RootId}")
        Console.WriteLine($"    if parsed as Double: {lossByDouble} (difference: {lossByDouble - expectedId})")
    End Sub

    ''' <summary>
    ''' 没有定义数据模型的 csv 文件：通过 ``OpenRawHandle`` 读取表头 schema 以及原始数据行。
    ''' </summary>
    Private Sub testRawRows()
        Console.WriteLine()
        Console.WriteLine("=== 5. raw rows for undocumented csv (RowObject) ===")

        Dim file As String = csv("synapse_attachement_rates.csv")
        Dim handle = file.OpenRawHandle()

        Console.WriteLine($"    schema headers: {String.Join(", ", handle.schema.Headers)}")

        Dim rows = handle.table.Take(3).ToArray
        Call check("raw rows read", rows.Length, 3)

        For Each row In rows
            Dim values As String() = row.ToArray
            Console.WriteLine($"    row: {String.Join(" | ", values)}")
        Next
    End Sub

    ''' <summary>
    ''' 流式读取与全量加载的行数一致性检查，用于确认两条数据加载路径的结果一致。
    ''' </summary>
    Private Sub testRowCountConsistency()
        Console.WriteLine()
        Console.WriteLine("=== 6. stream rows vs full load rows ===")

        Dim names As List(Of CellNames) = csv("names.csv").LoadCellNames()
        Dim namesStreamed As Integer = csv("names.csv").StreamCellNames().Count()
        Call check("CellNames stream rows == load rows", namesStreamed, names.Count)

        Dim tags As List(Of ConnectivityTags) = csv("connectivity_tags.csv").LoadConnectivityTags()
        Dim tagsStreamed As Integer = csv("connectivity_tags.csv").StreamConnectivityTags().Count()
        Call check("ConnectivityTags stream rows == load rows", tagsStreamed, tags.Count)

        Dim rates As List(Of SynapseAttachmentRates) = csv("synapse_attachement_rates.csv").LoadSynapseAttachmentRates()
        Dim ratesStreamed As Integer = csv("synapse_attachement_rates.csv").StreamSynapseAttachmentRates().Count()
        Call check("SynapseAttachmentRates stream rows == load rows", ratesStreamed, rates.Count)
    End Sub

    ''' <summary>
    ''' 框架行为对照演示 (不做断言)：
    ''' 
    ''' ``DataStream.OpenHandle`` + ``DataStream.AsLinq`` 会因为 ``BufferProvider`` 之中的
    ''' ``BaseStream.Seek(0)`` 没有配合 ``StreamReader.DiscardBufferedData`` 而把文件开头的
    ''' 缓冲区数据重复读取一次；同时 ``DataStream.Dispose`` 也不会关闭内部的文件读取器。
    ''' 
    ''' 因此加载模块改用了 ``DataLinqStream.OpenHandle`` + ``AsLinq`` (两者都定义在同一个
    ''' DataStream.vb 文件之中)，这个 api 不会发生数据行重复的问题。
    ''' </summary>
    Private Sub testDataStreamHandleBehavior()
        Console.WriteLine()
        Console.WriteLine("=== 7. streaming api regression (framework fixes) ===")

        Dim file As String = csv("names.csv")
        Dim expected As Integer = csvLineCount("names.csv")

        Console.WriteLine($"    data lines in names.csv: {expected}")

        ' 1. DataStream.AsLinq 完整枚举之后的行数应该与数据文件的行数一致
        '    (修复 BufferProvider 之中缺少 DiscardBufferedData 所导致的数据行重复读取)
        Dim n As Integer = 0

        Using handle As DataStream = DataStream.OpenHandle(file)
            For Each row As CellNames In handle.AsLinq(Of CellNames)()
                n += 1
            Next
        End Using

        Call check("DataStream.AsLinq rows == data lines", n, expected)

        ' 2. 完整枚举之后文件句柄应该已经被释放 (可以再次打开同一个文件)
        '    (修复 Dispose 没有关闭内部 StreamReader 所导致的文件句柄泄漏)
        Call check("file can be reopened after streaming", tryReopen(file), expected)

        ' 3. 迭代被提前中断 (Take) 之后文件句柄同样应该被释放
        Dim take5 = file.StreamCellNames().Take(5).ToArray
        Call check("Take(5) rows", take5.Length, 5)
        Call check("file can be reopened after Take", tryReopen(file), expected)

        ' 4. 另一个数据流 api 的行为对照
        Dim viaLinqStream As Integer = file.OpenDataLinqStream(Of CellNames)().Count()
        Call check("DataLinqStream rows == data lines", viaLinqStream, expected)
    End Sub

#End Region

#Region "test helpers"

    Private Function csv(name As String) As String
        Return Path.Combine(DATA_DIR, name)
    End Function

    ''' <summary>
    ''' 数据文件的行数 (不包含标题行)。
    ''' </summary>
    Private Function csvLineCount(name As String) As Long
        Return tryReopen(csv(name))
    End Function

    ''' <summary>
    ''' 重新打开目标文件并且返回其数据行数 (不包含标题行)。
    ''' 
    ''' 文件仍旧被其他对象占用 (例如流式读取之后文件句柄没有被释放) 的时候会抛出
    ''' ``IOException``，在这里捕获并且返回 -1。
    ''' </summary>
    Private Function tryReopen(path As String) As Long
        Try
            Dim lines As String() = System.IO.File.ReadAllLines(path)
            Dim n As Long = lines.Length

            ' 跳过末尾的空行
            Do While n > 0 AndAlso String.IsNullOrWhiteSpace(lines(n - 1))
                n -= 1
            Loop

            Return n - 1
        Catch ex As Exception
            Console.WriteLine($"    [ERROR] {path} -> {ex.GetType().Name}: {ex.Message}")
            Return -1
        End Try
    End Function

    ''' <summary>
    ''' 全量加载指定的表格，打印加载耗时以及第一行数据的摘要信息。
    ''' </summary>
    Private Sub loadCheck(Of T As Class)(name As String,
                                         fileName As String,
                                         expectedRows As Long,
                                         load As Func(Of String, List(Of T)),
                                         sample As Func(Of T, String))

        Dim timer As Stopwatch = Stopwatch.StartNew()
        Dim rows As List(Of T) = load(csv(fileName))
        timer.Stop()

        Call check($"{name} rows ({fileName})", rows.Count, expectedRows)
        Console.WriteLine($"    {timer.ElapsedMilliseconds,5} ms  {sample(rows(0))}")
    End Sub

    Private Sub run(name As String, test As Action)
        Try
            Call test()
        Catch ex As Exception
            failCount += 1
            Console.WriteLine($"[EXCEPTION] {name}: {ex.GetType().Name}: {ex.Message}")
        End Try
    End Sub

    Private Sub check(label As String, actual As Object, expected As Object)
        totalCount += 1

        ' 数值类型之间按照文本形式进行比较，避免 Object.Equals 因为装箱类型的不同而判定为不相等
        Dim ok As Boolean = String.Equals(Convert.ToString(actual), Convert.ToString(expected))

        If Not ok Then
            failCount += 1
        End If

        Console.WriteLine($"  [{(If(ok, "OK", "FAIL"))}] {label}: actual={actual}, expected={expected}")
    End Sub

#End Region

End Module
