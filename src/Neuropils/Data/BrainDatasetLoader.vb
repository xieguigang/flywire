Imports System.Globalization
Imports System.IO
Imports System.Threading
Imports FlywireAI.Connectome
Imports FlywireAI.FAFBv783
Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace Data

    ''' <summary>
    ''' 把 FAFB v783 的表格装配成 <see cref="BrainDataset"/>。
    ''' </summary>
    ''' <remarks>
    ''' 全部表格都以<b>流式</b>方式读取（``Stream*`` 扩展方法，惰性迭代、O(1) 内存），
    ''' 因为其中连接表有 261 MB / 534 万行、脑区表有 321 列。整个加载过程可以在后台线程
    ''' 上运行，并通过 <paramref name="progress"/> 与 <paramref name="cancel"/> 反馈进度
    ''' 与响应取消。
    ''' 
    ''' 数据来源与用途：
    ''' 
    ''' * ``names.csv`` → 神经元主索引 (<see cref="ConnectomeIndex"/>)；
    ''' * ``classification.csv`` / ``consolidated_cell_types.csv`` / ``neurons.csv`` → 注释；
    ''' * ``coordinates.csv`` → 每个神经元的三维坐标 (点云的位置)；
    ''' * ``neuropil_synapse_table.csv`` → 每个神经元的主导脑区 (颜色维度之一)；
    ''' * ``connections_princeton.csv`` → 突触连接 (三维连线)；
    ''' * ``neuron_activity_*.csv`` → 仿真活跃度 (可选，找不到时界面可以现场重跑仿真)。
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

        ''' <summary>
        ''' 加载整个数据集。
        ''' </summary>
        ''' <param name="progress">进度回调 (已经格式化的文本)</param>
        ''' <param name="cancel">取消标记</param>
        ''' <exception cref="FileNotFoundException">必需的表格不存在</exception>
        ''' <exception cref="OperationCanceledException">调用方取消了加载</exception>
        Public Function Load(Optional progress As Action(Of String) = Nothing,
                             Optional cancel As CancellationToken = Nothing) As BrainDataset

            Call validateFiles()

            Dim dataset As New BrainDataset With {
                .Source = m_config.DataDir
            }
            Dim clock As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            Dim stage As Long = 0

            ' 1) 神经元主索引 + 注释
            Call report(progress, $"loading neuron index from {m_config.NamesCsv} ...")

            Dim names As List(Of CellNames) = m_config.ResolvePath(m_config.NamesCsv).LoadCellNames()
            Dim index As New ConnectomeIndex()

            For Each cell As CellNames In names
                Call index.Add(cell.RootId)
            Next

            Call index.Freeze()

            Call report(progress, $"  {index.Size} neurons, attaching annotations ...")

            Call index.AttachAnnotations(names,
                                         m_config.ResolvePath(m_config.ClassificationCsv).LoadClassification(),
                                         m_config.ResolvePath(m_config.CellTypesCsv).LoadCellTypes(),
                                         m_config.ResolvePath(m_config.NeuronsCsv).LoadNeurons())

            dataset.Index = index

            ' 2) 递质类型 (ConnectomeIndex 只保留了"是否抑制性"，着色需要类型本身)
            Call throwIfCancelled(cancel)

            dataset.Neurotransmitters = loadNeurotransmitters(index, cancel)
            Call reportStage(progress, "neuron index + annotations", clock, stage)

            ' 3) 三维坐标
            Call throwIfCancelled(cancel)
            Call report(progress, $"loading coordinates from {m_config.CoordinatesCsv} ...")

            Call loadPositions(dataset, progress, cancel)
            Call reportStage(progress, "coordinates", clock, stage)

            ' 4) 主导脑区
            Call throwIfCancelled(cancel)
            Call report(progress, $"loading neuropil assignments from {m_config.NeuropilTableCsv} ...")

            Call loadNeuropils(dataset, progress, cancel)
            Call reportStage(progress, "neuropil assignment", clock, stage)

            ' 5) 连接表
            Call throwIfCancelled(cancel)
            Call report(progress, $"loading connections from {m_config.ConnectionsCsv} ...")

            Call loadConnections(dataset, progress, cancel)
            Call reportStage(progress, "connections", clock, stage)

            ' 6) 仿真活跃度 (可选)
            Call throwIfCancelled(cancel)
            Call loadActivity(dataset, progress)
            Call reportStage(progress, "activity (optional)", clock, stage)

            Call report(progress, $"dataset ready: {dataset}")

            Return dataset
        End Function

        ''' <summary>报告一个阶段的耗时 (同时也把逐阶段耗时写进自检报告)。</summary>
        Private Shared Sub reportStage(progress As Action(Of String), name As String, clock As Diagnostics.Stopwatch, ByRef stage As Long)
            Dim elapsed As Long = clock.ElapsedMilliseconds
            Dim used As Long = elapsed - stage

            stage = elapsed

            Call report(progress, $"  [{name}] {used} ms (total {elapsed} ms)")
        End Sub

#Region "loading steps"

        ''' <summary>每神经元的递质类型 (``neurons.csv``)。</summary>
        Private Function loadNeurotransmitters(index As ConnectomeIndex, cancel As CancellationToken) As String()
            Dim types As String() = New String(index.Size - 1) {}
            Dim i As Integer

            For Each cell As Neurons In m_config.ResolvePath(m_config.NeuronsCsv).StreamNeurons()
                Call throwIfCancelled(cancel)

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
        Private Sub loadPositions(dataset As BrainDataset, progress As Action(Of String), cancel As CancellationToken)
            Dim units As Integer = dataset.Units
            Dim index As ConnectomeIndex = dataset.Index
            Dim sums As Double() = New Double(units * 3 - 1) {}
            Dim marks As Integer() = New Integer(units - 1) {}
            Dim i As Integer
            Dim rows As Long = 0

            For Each cell As Coordinates In m_config.ResolvePath(m_config.CoordinatesCsv).StreamCoordinates()
                Call throwIfCancelled(cancel)

                rows += 1

                If (rows Mod 50000) = 0 Then
                    Call report(progress, $"  {rows} coordinate rows ...")
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

            Call report(progress, $"  {rows} coordinate rows -> {positioned}/{units} neurons have a position")
        End Sub

        ''' <summary>
        ''' 每个神经元的主导脑区：在 321 列的脑区表里取"输入+输出突触数最多"的脑区。
        ''' </summary>
        ''' <remarks>
        ''' 列名通过 <see cref="ColumnAttribute"/> 反射得到（``input synapses in AL_L`` 之类），
        ''' 属性名本身被简写成 VB 标识符，只有列名保留着脑区前缀。取值走
        ''' <see cref="Delegate.CreateDelegate(Type, MethodInfo)"/> 绑定出来的委托而不是
        ''' ``PropertyInfo.GetValue``：134,181 行 × 160 列 = 2100 万次读取，反射调用会慢一个数量级。
        ''' </remarks>
        Private Sub loadNeuropils(dataset As BrainDataset, progress As Action(Of String), cancel As CancellationToken)
            Dim path As String = m_config.ResolvePath(m_config.NeuropilTableCsv)
            Dim units As Integer = dataset.Units
            Dim assignment As Integer() = New Integer(units - 1) {}
            Dim strength As Double() = New Double(units - 1) {}
            Dim rows As Long = 0
            Dim assigned As Integer = 0
            Dim canceled As Boolean = False

            For k As Integer = 0 To assignment.Length - 1
                assignment(k) = -1
            Next

            ' 表头 -> 需要的列位置 (321 列里只取 input/output synapses in <region> 这 160 列)
            Dim header As String() = Nothing
            Dim columnPositions As Integer() = Nothing
            Dim regionSlots As Integer() = Nothing
            Dim regionNames As String() = Nothing
            Dim rootColumn As Integer = 0

            For Each line As String In IO.File.ReadLines(path)
                If header Is Nothing Then
                    header = line.Split(","c)

                    Dim parsed = parseNeuropilHeader(header)

                    columnPositions = parsed.Columns
                    regionSlots = parsed.Regions
                    regionNames = parsed.Names
                    rootColumn = parsed.RootColumn

                    Exit For
                End If
            Next

            If regionNames Is Nothing OrElse regionNames.Length = 0 Then
                Throw New InvalidDataException($"脑区表缺少 ``... synapses in <region>`` 列: {path}")
            End If

            Dim totals As Double() = New Double(regionNames.Length - 1) {}
            Dim firstLine As Boolean = True

            For Each line As String In IO.File.ReadLines(path)
                If canceled Then Exit For

                If firstLine Then
                    firstLine = False
                    Continue For
                End If

                rows += 1

                If (rows Mod 20000) = 0 Then
                    If cancel.IsCancellationRequested Then
                        canceled = True
                        Exit For
                    End If

                    Call report(progress, $"  {rows} neuropil rows ...")
                End If

                If line.Length = 0 Then Continue For

                Dim parts As String() = line.Split(","c)

                If parts.Length <= rootColumn Then Continue For

                Dim rootId As Long

                If Not Long.TryParse(parts(rootColumn), rootId) Then Continue For

                Dim i As Integer

                If Not dataset.Index.IndexOf(rootId, i) Then Continue For

                Array.Clear(totals, 0, totals.Length)

                For c As Integer = 0 To columnPositions.Length - 1
                    Dim value As Double

                    If Double.TryParse(parts(columnPositions(c)), NumberStyles.Float, CultureInfo.InvariantCulture, value) Then
                        totals(regionSlots(c)) += value
                    End If
                Next

                Dim best As Integer = -1
                Dim bestValue As Double = 0

                For r As Integer = 0 To totals.Length - 1
                    If totals(r) > bestValue Then
                        bestValue = totals(r)
                        best = r
                    End If
                Next

                If best >= 0 Then
                    assignment(i) = best
                    strength(i) = bestValue
                    assigned += 1
                End If
            Next

            If canceled Then Throw New OperationCanceledException(cancel)

            dataset.Neuropil = assignment
            dataset.NeuropilNames = regionNames
            dataset.NeuropilSynapses = strength

            Call report(progress, $"  {rows} rows -> {assigned}/{units} neurons assigned to {regionNames.Length} neuropils")
        End Sub

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
        ''' 连接表：``pre_root_id, post_root_id, neuropil, syn_count, nt_type``。
        ''' </summary>
        Private Sub loadConnections(dataset As BrainDataset, progress As Action(Of String), cancel As CancellationToken)
            Dim index As ConnectomeIndex = dataset.Index
            Dim capacity As Integer = System.Math.Min(MaxConnections, 5400000)

            Dim pre As New List(Of Integer)(capacity)
            Dim post As New List(Of Integer)(capacity)
            Dim synapses As New List(Of Integer)(capacity)
            Dim neuropils As New List(Of Integer)(capacity)
            Dim types As New List(Of Integer)(capacity)

            Dim neuropilNames As New List(Of String)()
            Dim neuropilIds As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
            Dim ntNames As New List(Of String)()
            Dim ntIds As New Dictionary(Of String, Integer)(StringComparer.Ordinal)

            Dim rows As Long = 0
            Dim skipped As Long = 0
            Dim truncated As Boolean = False
            Dim firstLine As Boolean = True
            Dim canceled As Boolean = False

            ' 逐行按字段位置取值，而不是走框架的反射映射：
            ' 534 万行 × 5 列在反射映射下实测 32 s，这里约 4 s。
            ' 该表的字段都是整数与短标识符 (没有引号包裹的逗号)，因此按位置切分是安全的。
            For Each line As String In IO.File.ReadLines(m_config.ResolvePath(m_config.ConnectionsCsv))
                If firstLine Then
                    firstLine = False
                    Continue For
                End If

                rows += 1

                If (rows Mod 200000) = 0 Then
                    If cancel.IsCancellationRequested Then
                        canceled = True
                        Exit For
                    End If

                    Call report(progress, $"  {rows} connection rows, {pre.Count} accepted ... ({m_config.ConnectionsCsv})")
                End If

                If line.Length = 0 Then
                    Continue For
                End If

                Dim parts As String() = line.Split(","c)

                If parts.Length < 5 Then
                    skipped += 1
                    Continue For
                End If

                Dim preRoot As Long
                Dim postRoot As Long

                If Not Long.TryParse(parts(0), preRoot) OrElse Not Long.TryParse(parts(1), postRoot) Then
                    skipped += 1
                    Continue For
                End If

                Dim preIndex As Integer
                Dim postIndex As Integer

                ' 索引在注释表建立之后已经冻结，连接表里出现的其它 root_id 只能跳过
                If Not index.IndexOf(preRoot, preIndex) Then
                    skipped += 1
                    Continue For
                End If
                If Not index.IndexOf(postRoot, postIndex) Then
                    skipped += 1
                    Continue For
                End If

                Dim synCount As Double

                Call Double.TryParse(parts(3), NumberStyles.Float, CultureInfo.InvariantCulture, synCount)

                Call pre.Add(preIndex)
                Call post.Add(postIndex)
                Call synapses.Add(CInt(System.Math.Max(0, System.Math.Min(Integer.MaxValue, synCount))))
                Call neuropils.Add(intern(parts(2), neuropilNames, neuropilIds))
                Call types.Add(intern(parts(4), ntNames, ntIds))

                If pre.Count >= MaxConnections Then
                    truncated = True
                    Exit For
                End If
            Next

            If canceled Then Throw New OperationCanceledException(cancel)

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

        Private Sub validateFiles()
            Dim required As String() = {
                m_config.NamesCsv,
                m_config.ClassificationCsv,
                m_config.CellTypesCsv,
                m_config.NeuronsCsv,
                m_config.CoordinatesCsv,
                m_config.NeuropilTableCsv,
                m_config.ConnectionsCsv
            }
            Dim missing As New List(Of String)()

            For Each name As String In required
                Dim path As String = m_config.ResolvePath(name)

                ' 注意：循环变量不能叫 file，否则会遮蔽 System.IO.File (VB 不区分大小写)
                If Not IO.File.Exists(path) Then
                    Call missing.Add(path)
                End If
            Next

            If missing.Count > 0 Then
                Throw New FileNotFoundException(
                    $"数据文件缺失 (DataDir = {m_config.DataDir}):{Environment.NewLine}  " &
                    String.Join(Environment.NewLine & "  ", missing))
            End If
        End Sub

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
