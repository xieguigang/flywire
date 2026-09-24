Imports System.Globalization
Imports System.IO
Imports System.Threading
Imports FlywireAI.Connectome
Imports FlywireAI.FAFBv783
Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace Data

    ''' <summary>
    ''' 把 FAFB v783 的 <b>msgpack 转储包</b>装配成 <see cref="BrainDataset"/>。
    ''' </summary>
    ''' <remarks>
    ''' <b>数据源只有一个：msgpack 转储包</b>（见 <see cref="FafbMsgPackStorage"/>，
    ''' 默认位于 ``&lt;DataDir&gt;\fafb-v783.msgpack.zip``）。本加载器<b>不再读任何 csv</b>：
    ''' 包里已经是解析好的定型数组（root_id / 坐标 / 脑区计数 / 连接列），
    ''' 读回来只剩二进制解码，没有 ASCII 文本解析（实测 2.5 s vs csv 路径的 7.6 s）。
    ''' 
    ''' 包不存在 / 损坏 / 版本不对 / 源 csv 被改过（过期）时<b>直接抛异常</b>并给出
    ''' ``Neuropils.exe --dump`` 的提示，而不是悄悄回退到 csv ——
    ''' 静默回退会让"到底读的是哪一份数据"变得无法确定。
    ''' 
    ''' 包内的条目与用途：
    ''' 
    ''' * ``names`` → 神经元主索引 (<see cref="ConnectomeIndex"/>)；
    ''' * ``classification`` / ``cell_types`` / ``neurons`` → 注释；
    ''' * ``coordinates`` → 每个神经元的三维坐标 (点云的位置)；
    ''' * ``neuropil`` + 321 个列项 → 每个神经元的主导脑区 (颜色维度之一)；
    ''' * ``connections`` + 5 个列项 → 突触连接 (三维连线，也是电刺激仿真的连接组)。
    ''' 
    ''' 仿真活跃度 (``neuron_activity_*.csv``) 是仿真产生的<b>结果</b>而不是数据源，
    ''' 因此仍然按原来的方式从 ``snn-output`` 里读；找不到时界面可以现场重跑一次仿真。
    ''' </remarks>
    Public Class BrainDatasetLoader

        ''' <summary>连接表的默认读取上限 (534 万行 × 5 个并行数组约 100 MB)。</summary>
        Public Const DefaultMaxConnections As Integer = 6000000

        Private ReadOnly m_config As VisualizationConfig

        Public Sub New(config As VisualizationConfig)
            If config Is Nothing Then Throw New ArgumentNullException(NameOf(config))

            m_config = config
        End Sub

        ''' <summary>连接表的读取上限 (超过之后截断，并在进度里报告)。</summary>
        Public Property MaxConnections As Integer = DefaultMaxConnections

        ''' <summary>转储包的路径；为空时落到 <see cref="FafbMsgPackStorage.ResolvePackFile"/> 的默认位置。</summary>
        Public Property PackFile As String = Nothing

        ''' <summary>
        ''' 从 msgpack 转储包加载整个数据集（唯一的数据源）。
        ''' </summary>
        ''' <param name="progress">进度回调 (已经格式化的文本)</param>
        ''' <param name="cancel">取消标记</param>
        ''' <exception cref="InvalidDataException">转储包不存在 / 损坏 / 版本不对 / 已过期</exception>
        ''' <exception cref="OperationCanceledException">调用方取消了加载</exception>
        Public Function Load(Optional progress As Action(Of String) = Nothing,
                             Optional cancel As CancellationToken = Nothing) As BrainDataset

            Dim packPath As String = FafbMsgPackStorage.ResolvePackFile(m_config, PackFile)
            Dim reason As String = FafbMsgPackStorage.Verify(m_config, PackFile)

            If Not String.IsNullOrEmpty(reason) Then
                Throw New InvalidDataException(
                    $"无法使用 msgpack 转储包：{reason}{Environment.NewLine}" &
                    $"  若还没有转储包，可以先执行一次转储（这一句只读一次 csv，之后界面就只认转储包）：{Environment.NewLine}" &
                    $"    Neuropils.exe --dump ""{packPath}"" ""{m_config.DataDir}""")
            End If

            Call report(progress, $"loading from the msgpack archive {packPath} ...")

            Using reader As FafbPackReader = FafbMsgPackStorage.Open(packPath)
                Return loadFromPack(reader, progress, cancel)
            End Using
        End Function

        ''' <summary>报告一个阶段的耗时 (同时也把逐阶段耗时写进自检报告)。</summary>
        Private Shared Sub reportStage(progress As Action(Of String), name As String, clock As Diagnostics.Stopwatch, ByRef stage As Long)
            Dim elapsed As Long = clock.ElapsedMilliseconds
            Dim used As Long = elapsed - stage

            stage = elapsed

            Call report(progress, $"  [{name}] {used} ms (total {elapsed} ms)")
        End Sub

#Region "loading steps"

        ''' <summary>转储包里 ``neurons`` 的记录 → 每神经元一个递质字符串。</summary>
        Private Shared Function neurotransmittersOf(index As ConnectomeIndex, rows As List(Of Neurons)) As String()
            Dim types As String() = New String(index.Size - 1) {}
            Dim i As Integer

            For Each cell As Neurons In rows
                If cell Is Nothing OrElse Not index.IndexOf(cell.RootId, i) Then
                    Continue For
                End If

                types(i) = If(cell.NtType, "")
            Next

            ' 空值统一成一个可显示的名字，避免界面上出现空白图例项
            For k As Integer = 0 To types.Length - 1
                If String.IsNullOrWhiteSpace(types(k)) Then
                    types(k) = "unknown"
                End If
            Next

            Return types
        End Function

        ''' <summary>
        ''' 每个神经元的三维坐标：``coordinates.csv`` 对同一个神经元可能有多条标记位置，
        ''' 这里取它们的算术平均 (并记录标记条数)。
        ''' </summary>
        ''' <remarks>转储包里的坐标记录 → 每神经元一个平均位置。</remarks>>
        Private Shared Sub applyPositions(dataset As BrainDataset, rows As List(Of Coordinates), progress As Action(Of String))
            Dim units As Integer = dataset.Units
            Dim index As ConnectomeIndex = dataset.Index
            Dim sums As Double() = New Double(units * 3 - 1) {}
            Dim marks As Integer() = New Integer(units - 1) {}
            Dim i As Integer
            Dim total As Long = 0

            For Each cell As Coordinates In rows
                total += 1

                If (total Mod 50000) = 0 Then
                    Call report(progress, $"  {total} coordinate rows ...")
                End If

                If cell Is Nothing OrElse Not index.IndexOf(cell.RootId, i) Then
                    Continue For
                End If

                Dim position As NeuronPosition

                If Not PositionParser.TryParse(cell.Position, position) Then
                    Continue For
                End If

                ' 全零坐标是占位值 (没有标记位置)，不计入平均
                If position.IsOrigin Then
                    Continue For
                End If

                sums(i * 3) += position.X
                sums(i * 3 + 1) += position.Y
                sums(i * 3 + 2) += position.Z
                marks(i) += 1
            Next

            Dim positions As Double() = New Double(units * 3 - 1) {}
            Dim positioned As Integer = 0

            For k As Integer = 0 To units - 1
                If marks(k) > 0 Then
                    positions(k * 3) = sums(k * 3) / marks(k)
                    positions(k * 3 + 1) = sums(k * 3 + 1) / marks(k)
                    positions(k * 3 + 2) = sums(k * 3 + 2) / marks(k)
                    positioned += 1
                Else
                    positions(k * 3) = Double.NaN
                    positions(k * 3 + 1) = Double.NaN
                    positions(k * 3 + 2) = Double.NaN
                End If
            Next

            dataset.Positions = positions
            dataset.PositionMarks = marks

            Call report(progress, $"  {total} coordinate rows -> {positioned}/{units} neurons have a position")
        End Sub

        ''' <summary>
        ''' 在"各脑区的输入+输出突触数"里挑出最大的那个脑区（转储包路径使用）。
        ''' </summary>
        ''' <returns>脑区下标与它的突触数；全部为 0 时返回 <c>-1</c>。</returns>
        Private Shared Function dominantRegion(totals As Double()) As (Region As Integer, Value As Double)
            Dim best As Integer = -1
            Dim bestValue As Double = 0

            For r As Integer = 0 To totals.Length - 1
                If totals(r) > bestValue Then
                    bestValue = totals(r)
                    best = r
                End If
            Next

            Return (best, bestValue)
        End Function

        ''' <summary>
        ''' 从脑区表的表头里挑出 ``input/output synapses in &lt;region&gt;`` 列的位置。
        ''' </summary>
        ''' <remarks>
        ''' 只解析需要的 160 列：整张表有 321 列，逐列走反射映射实测要 32 s，
        ''' 按位置取值并只解析这 160 列约 1.5 s。
        ''' </remarks>
        Private Shared Function parseNeuropilHeader(header As String()) As (Columns As Integer(), Regions As Integer(), Names As String(), RootColumn As Integer)
            Const InputPrefix As String = "input synapses in "
            Const OutputPrefix As String = "output synapses in "

            Dim columns As New List(Of Integer)()
            Dim regions As New List(Of Integer)()
            Dim names As New List(Of String)()
            Dim ids As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
            Dim rootColumn As Integer = 0

            For c As Integer = 0 To header.Length - 1
                Dim column As String = header(c).Trim()

                If String.Equals(column, "root_id", StringComparison.OrdinalIgnoreCase) Then
                    rootColumn = c
                    Continue For
                End If

                Dim region As String

                If column.StartsWith(InputPrefix, StringComparison.OrdinalIgnoreCase) Then
                    region = column.Substring(InputPrefix.Length).Trim()
                ElseIf column.StartsWith(OutputPrefix, StringComparison.OrdinalIgnoreCase) Then
                    region = column.Substring(OutputPrefix.Length).Trim()
                Else
                    Continue For
                End If

                ' 同一个脑区有"输入突触"与"输出突触"两列，累加到同一个脑区槽位
                Dim regionIndex As Integer

                If Not ids.TryGetValue(region, regionIndex) Then
                    regionIndex = names.Count
                    Call names.Add(region)
                    Call ids.Add(region, regionIndex)
                End If

                Call columns.Add(c)
                Call regions.Add(regionIndex)
            Next

            Return (columns.ToArray(), regions.ToArray(), names.ToArray(), rootColumn)
        End Function

        ''' <summary>
        ''' 仿真活跃度：读取 ``neuron_activity_*.csv`` (全量逐神经元脉冲计数)。
        ''' </summary>
        ''' <remarks>
        ''' 找不到时不做任何事：界面可以在需要的时候现场跑一次全脑仿真 (GPU 下只要几十毫秒)，
        ''' 那条路径拿到的活跃度与这里的文件完全同源 (都是 <c>BrainSimulationResult.Counts</c>)。
        ''' </remarks>
        Private Sub loadActivity(dataset As BrainDataset, progress As Action(Of String))
            Dim dir As String = m_config.ResolveActivityDir()

            If Not Directory.Exists(dir) Then
                Return
            End If

            ' 报告是写到 snn-output\<时间戳>\ 子目录里的，所以要递归搜索，
            ' 并取最近一次写入的那一份 (同一目录下会有多轮仿真的结果)
            Dim latest As String = Directory _
                .EnumerateFiles(dir, "neuron_activity_*.csv", SearchOption.AllDirectories) _
                .OrderByDescending(Function(f) File.GetLastWriteTimeUtc(f)) _
                .FirstOrDefault()

            If latest Is Nothing Then
                Call report(progress, $"no activity table found under {dir}")

                Return
            End If

            Dim counts As Double() = New Double(dataset.Units - 1) {}
            Dim accepted As Integer = 0

            ' 表头: neuron_index,root_id,spike_count,firing_rate
            For Each line As String In File.ReadLines(latest).Skip(1)
                If String.IsNullOrWhiteSpace(line) Then
                    Continue For
                End If

                Dim parts As String() = line.Split(","c)

                If parts.Length < 3 Then
                    Continue For
                End If

                Dim i As Integer

                If Not Integer.TryParse(parts(0), i) OrElse i < 0 OrElse i >= counts.Length Then
                    Continue For
                End If

                counts(i) = Double.Parse(parts(2), NumberStyles.Float, CultureInfo.InvariantCulture)
                accepted += 1
            Next

            If accepted > 0 Then
                dataset.Activity = counts
                dataset.ActivitySource = Path.GetFileName(latest)

                Call report(progress, $"  activity loaded from {dataset.ActivitySource} ({accepted} rows)")
            End If
        End Sub

#End Region

#Region "从 msgpack 转储包装配"

        ''' <summary>
        ''' 从 msgpack 转储包装配数据集 —— 这是<b>唯一</b>的装配路径。
        ''' </summary>
        ''' <remarks>
        ''' 每一步的语义（神经元主索引、坐标取平均、脑区取最大值、连接按索引过滤）
        ''' 与原来读 csv 时完全一致，只是"记录从哪儿来"换成了转储包里读回来的定型数组。
        ''' </remarks>
        Private Function loadFromPack(reader As FafbPackReader,
                                      progress As Action(Of String),
                                      cancel As CancellationToken) As BrainDataset

            Dim dataset As New BrainDataset With {
                .Source = m_config.DataDir
            }
            Dim clock As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            Dim stage As Long = 0

            ' 1) 神经元主索引 + 注释
            Call throwIfCancelled(cancel)
            Call report(progress, $"loading neuron index from {FafbMsgPackStorage.EntryNameOf(FafbMsgPackStorage.KeyNames)} ...")

            Dim namesPack As CellNamesPack = reader.Read(Of CellNamesPack)(FafbMsgPackStorage.KeyNames)
            Dim neuronsPack As NeuronsPack = reader.Read(Of NeuronsPack)(FafbMsgPackStorage.KeyNeurons)
            Dim names As List(Of CellNames) = If(namesPack Is Nothing, New List(Of CellNames)(), namesPack.ToRecords())
            Dim neurons As List(Of Neurons) = If(neuronsPack Is Nothing, New List(Of Neurons)(), neuronsPack.ToRecords())
            Dim index As New ConnectomeIndex()

            For Each cell As CellNames In names
                Call index.Add(cell.RootId)
            Next

            Call index.Freeze()

            Call report(progress, $"  {index.Size} neurons, attaching annotations ...")

            Dim classificationPack As ClassificationPack = reader.Read(Of ClassificationPack)(FafbMsgPackStorage.KeyClassification)
            Dim cellTypesPack As CellTypesPack = reader.Read(Of CellTypesPack)(FafbMsgPackStorage.KeyCellTypes)

            Call index.AttachAnnotations(
                names,
                If(classificationPack Is Nothing, New List(Of Classification)(), classificationPack.ToRecords()),
                If(cellTypesPack Is Nothing, New List(Of CellTypes)(), cellTypesPack.ToRecords()),
                neurons)

            dataset.Index = index

            ' 2) 递质类型 (ConnectomeIndex 只保留了"是否抑制性"，着色需要类型本身)
            Call throwIfCancelled(cancel)

            dataset.Neurotransmitters = neurotransmittersOf(index, neurons)
            Call reportStage(progress, "neuron index + annotations", clock, stage)

            ' 3) 三维坐标
            Call throwIfCancelled(cancel)
            Call report(progress, $"loading coordinates from {FafbMsgPackStorage.EntryNameOf(FafbMsgPackStorage.KeyCoordinates)} ...")

            Dim coordinatesPack As CoordinatesPack = reader.Read(Of CoordinatesPack)(FafbMsgPackStorage.KeyCoordinates)

            Call applyPositions(dataset,
                                If(coordinatesPack Is Nothing, New List(Of Coordinates)(), coordinatesPack.ToRecords()),
                                progress)
            Call reportStage(progress, "coordinates", clock, stage)

            ' 4) 主导脑区
            Call throwIfCancelled(cancel)
            Call report(progress, $"loading neuropil assignments from {FafbMsgPackStorage.EntryNameOf(FafbMsgPackStorage.KeyNeuropil)} ...")

            Call assignNeuropilsFromPack(dataset, reader,
                                         reader.Read(Of NeuropilTablePack)(FafbMsgPackStorage.KeyNeuropil), progress)
            Call reportStage(progress, "neuropil assignment", clock, stage)

            ' 5) 连接表
            Call throwIfCancelled(cancel)
            Call report(progress, $"loading connections from {FafbMsgPackStorage.EntryNameOf(FafbMsgPackStorage.KeyConnections)} ...")

            Call loadConnectionsFromPack(dataset, reader,
                                         reader.Read(Of ConnectionsPack)(FafbMsgPackStorage.KeyConnections),
                                         progress, cancel)
            Call reportStage(progress, "connections", clock, stage)

            ' 6) 仿真活跃度 (可选)
            Call throwIfCancelled(cancel)
            Call loadActivity(dataset, progress)
            Call reportStage(progress, "activity (optional)", clock, stage)

            Call report(progress, $"dataset ready: {dataset}")

            Return dataset
        End Function

        ''' <summary>
        ''' 主导脑区：按 ``input/output synapses in &lt;region&gt;`` 挑出需要的列，
        ''' 再取每个神经元"输入+输出突触数"最大的那个脑区；数值来自转储包的列式数组。
        ''' </summary>
        Private Shared Sub assignNeuropilsFromPack(dataset As BrainDataset, reader As FafbPackReader,
                                                   pack As NeuropilTablePack, progress As Action(Of String))
            If pack Is Nothing Then Throw New InvalidDataException("转储包里没有脑区表 (neuropil)")

            Dim units As Integer = dataset.Units
            Dim assignment As Integer() = New Integer(units - 1) {}
            Dim strength As Double() = New Double(units - 1) {}

            For k As Integer = 0 To assignment.Length - 1
                assignment(k) = -1
            Next

            Dim parsed = parseNeuropilHeader(pack.Columns)
            Dim regionNames As String() = parsed.Names

            If regionNames Is Nothing OrElse regionNames.Length = 0 Then
                Throw New InvalidDataException("转储包里的脑区表缺少 ``... synapses in <region>`` 列")
            End If

            ' 只把"用得上的那 158 个脑区突触数列"读进来：
            ' 321 列整读一遍要比只挑出需要的列多花一个数量级的时间
            Dim columns As Double()() = New Double(parsed.Columns.Length - 1)() {}

            For c As Integer = 0 To parsed.Columns.Length - 1
                columns(c) = reader.ReadDoubles(FafbMsgPackStorage.NeuropilColumnKey(parsed.Columns(c)))
            Next

            Dim totals As Double() = New Double(regionNames.Length - 1) {}
            Dim rows As Integer = pack.RowCount()
            Dim assigned As Integer = 0
            Dim i As Integer

            For r As Integer = 0 To rows - 1
                If Not dataset.Index.IndexOf(pack.RootId(r), i) Then
                    Continue For
                End If

                Array.Clear(totals, 0, totals.Length)

                For c As Integer = 0 To parsed.Columns.Length - 1
                    totals(parsed.Regions(c)) += columns(c)(r)
                Next

                Dim best As (Region As Integer, Value As Double) = dominantRegion(totals)

                If best.Region >= 0 Then
                    assignment(i) = best.Region
                    strength(i) = best.Value
                    assigned += 1
                End If
            Next

            dataset.Neuropil = assignment
            dataset.NeuropilNames = regionNames
            dataset.NeuropilSynapses = strength

            Call report(progress, $"  {rows} rows -> {assigned}/{units} neurons assigned to {regionNames.Length} neuropils")
        End Sub

        ''' <summary>
        ''' 连接表：转储包里已经字典化的分类列在这里重新编号，
        ''' 保证与 csv 路径得到<b>完全相同</b>的名称表 (按首次出现的顺序)。
        ''' </summary>
        Private Sub loadConnectionsFromPack(dataset As BrainDataset, reader As FafbPackReader, pack As ConnectionsPack,
                                            progress As Action(Of String), cancel As CancellationToken)
            If pack Is Nothing Then Throw New InvalidDataException("转储包里没有连接表 (connections)")

            Dim index As ConnectomeIndex = dataset.Index
            Dim rows As Integer = pack.RowCount
            Dim capacity As Integer = System.Math.Min(MaxConnections, rows)

            ' 5 个数据列走快通道整段读回（逐元素反序列化 2,670 万个值要比解析 csv 还慢）
            Dim preRoots As Long() = reader.ReadLongs(FafbMsgPackStorage.ConnectionColumnKey(FafbMsgPackStorage.ColumnPre))
            Dim postRoots As Long() = reader.ReadLongs(FafbMsgPackStorage.ConnectionColumnKey(FafbMsgPackStorage.ColumnPost))
            Dim neuropilCodes As Integer() = reader.ReadIntegers(FafbMsgPackStorage.ConnectionColumnKey(FafbMsgPackStorage.ColumnNeuropil))
            Dim synCounts As Double() = reader.ReadDoubles(FafbMsgPackStorage.ConnectionColumnKey(FafbMsgPackStorage.ColumnSynapses))
            Dim ntCodes As Integer() = reader.ReadIntegers(FafbMsgPackStorage.ConnectionColumnKey(FafbMsgPackStorage.ColumnNt))

            If preRoots Is Nothing OrElse postRoots Is Nothing Then
                Throw New InvalidDataException("转储包里的连接表缺少 pre / post 列")
            End If

            Dim pre As New List(Of Integer)(capacity)
            Dim post As New List(Of Integer)(capacity)
            Dim synapses As New List(Of Integer)(capacity)
            Dim neuropils As New List(Of Integer)(capacity)
            Dim types As New List(Of Integer)(capacity)

            Dim neuropilNames As New List(Of String)()
            Dim neuropilIds As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
            Dim ntNames As New List(Of String)()
            Dim ntIds As New Dictionary(Of String, Integer)(StringComparer.Ordinal)

            Dim skipped As Long = 0
            Dim truncated As Boolean = False

            For r As Integer = 0 To rows - 1
                If r > 0 AndAlso (r Mod 500000) = 0 Then
                    Call throwIfCancelled(cancel)
                    Call report(progress, $"  {r} connection rows, {pre.Count} accepted ... (msgpack)")
                End If

                Dim preIndex As Integer
                Dim postIndex As Integer

                ' 索引在注释表建立之后已经冻结，转储包里出现的其它 root_id 只能跳过
                If Not index.IndexOf(preRoots(r), preIndex) Then
                    skipped += 1

                    Continue For
                End If
                If Not index.IndexOf(postRoots(r), postIndex) Then
                    skipped += 1

                    Continue For
                End If

                Call pre.Add(preIndex)
                Call post.Add(postIndex)
                Call synapses.Add(CInt(System.Math.Max(0, System.Math.Min(Integer.MaxValue, synCounts(r)))))
                Call neuropils.Add(intern(pack.NeuropilOf(code(neuropilCodes, r)), neuropilNames, neuropilIds))
                Call types.Add(intern(pack.NeurotransmitterOf(code(ntCodes, r)), ntNames, ntIds))

                If pre.Count >= MaxConnections Then
                    truncated = True

                    Exit For
                End If
            Next

            dataset.Pre = pre.ToArray()
            dataset.Post = post.ToArray()
            dataset.SynCount = synapses.ToArray()
            dataset.ConnectionNeuropil = neuropils.ToArray()
            dataset.ConnectionNtType = types.ToArray()
            dataset.ConnectionNeuropils = neuropilNames.ToArray()
            dataset.ConnectionNeurotransmitters = ntNames.ToArray()

            Call report(progress, $"  {rows} rows -> {dataset.ConnectionCount} connections " &
                                  $"({skipped} skipped, {neuropilNames.Count} neuropils, {ntNames.Count} neurotransmitters)")

            If truncated Then
                Call report(progress, $"  [WARN] the connection table was truncated at {MaxConnections} rows")
            End If
        End Sub

#End Region

        ''' <summary>取字典化列的第 i 个编号 (缺列 / 越界时返回 -1，交由上层当"(unknown)"处理)。</summary>
        Private Shared Function code(codes As Integer(), i As Integer) As Integer
            If codes Is Nothing OrElse i < 0 OrElse i >= codes.Length Then Return -1

            Return codes(i)
        End Function

        ''' <summary>把名称字符串映射到连续索引 (保持首次出现的顺序)。</summary>
        Private Shared Function intern(name As String, names As List(Of String), ids As Dictionary(Of String, Integer)) As Integer
            Dim text As String = If(String.IsNullOrWhiteSpace(name), "(unknown)", name.Trim)
            Dim id As Integer

            If ids.TryGetValue(text, id) Then
                Return id
            End If

            id = names.Count
            Call names.Add(text)
            Call ids.Add(text, id)

            Return id
        End Function

        Private Shared Sub throwIfCancelled(cancel As CancellationToken)
            If cancel.IsCancellationRequested Then
                Throw New OperationCanceledException(cancel)
            End If
        End Sub

        Private Shared Sub report(progress As Action(Of String), message As String)
            If progress Is Nothing Then Return

            Call progress(message)
        End Sub

    End Class

End Namespace
