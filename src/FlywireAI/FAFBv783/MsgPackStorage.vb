Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports FlywireAI.Connectome
Imports Microsoft.VisualBasic.Data.IO.MessagePack
Imports Microsoft.VisualBasic.Data.IO.MessagePack.Serialization

Namespace FAFBv783

    ''' <summary>转储包里"某一个数据源文件"的指纹（用于判断转储包是否过期）。</summary>
    Public Class FafbPackSource

        ''' <summary>转储包内的键名 (例如 ``names`` / ``connections``)。</summary>
        <MessagePackMember(1)>
        Public Property Key As String

        ''' <summary>源文件名 (相对于 <see cref="SnnConfig.DataDir"/>)。</summary>
        <MessagePackMember(2)>
        Public Property FileName As String

        ''' <summary>源文件字节数。</summary>
        <MessagePackMember(3)>
        Public Property Length As Long

        ''' <summary>源文件最后写入时间 (UTC ticks；用 ticks 而不是 Date 是为了精确比对)。</summary>
        <MessagePackMember(4)>
        Public Property LastWriteTicks As Long

        ''' <summary>转储出来的行数。</summary>
        <MessagePackMember(5)>
        Public Property Rows As Integer

        ''' <summary>转储这一步的耗时 (毫秒)。</summary>
        <MessagePackMember(6)>
        Public Property ElapsedMs As Long

    End Class

    ''' <summary>
    ''' 转储包的清单 (``manifest.msgpack``)：版本 + 每个数据源文件的指纹。
    ''' </summary>
    ''' <remarks>
    ''' 指纹的用途：<b>源 csv 一旦被换掉，转储包就必须作废</b>，否则界面会读到一个
    ''' 与磁盘上的数据不一致的缓存。比对的是"文件名 + 字节数 + 最后写入时间"。
    ''' </remarks>
    Public Class FafbPackManifest

        ''' <summary>转储格式版本；对不上就当作过期包。</summary>
        <MessagePackMember(1)>
        Public Property FormatVersion As Integer

        ''' <summary>转储时的数据目录。</summary>
        <MessagePackMember(2)>
        Public Property DataDir As String

        ''' <summary>转储时间 (UTC，ISO8601 字符串)。</summary>
        <MessagePackMember(3)>
        Public Property CreatedUtc As String

        ''' <summary>整个转储过程的耗时 (毫秒)。</summary>
        <MessagePackMember(4)>
        Public Property ElapsedMs As Long

        ''' <summary>每个数据源文件的指纹。</summary>
        <MessagePackMember(5)>
        Public Property Sources As FafbPackSource()

        Public Function Describe() As String
            Dim text As New Text.StringBuilder()

            Call text.AppendLine($"format version {FormatVersion}, created at {CreatedUtc}, {ElapsedMs} ms")

            If Sources IsNot Nothing Then
                For Each source As FafbPackSource In Sources
                    Call text.AppendLine($"  {source.Key,-14} {source.Rows,10:N0} rows  <- {source.FileName} ({source.ElapsedMs} ms)")
                Next
            End If

            Return text.ToString()
        End Function

    End Class

    ''' <summary>
    ''' 转储包的<b>只读</b>访问器：按下标读回某一张表的 msgpack。
    ''' </summary>
    ''' <remarks>
    ''' 每一条 zip entry 都是一份完整的 msgpack 文档，因此每次读取都单独开流、读完即关
    ''' （msgpack 的反序列化器内部会把流包进 <c>BinaryDataReader</c>，不能在同一条流上连读两次）。
    ''' </remarks>
    Public Class FafbPackReader
        Implements IDisposable

        Private ReadOnly m_zip As ZipArchive
        Private m_disposed As Boolean

        ''' <summary>包里的清单。</summary>
        Public ReadOnly Property Manifest As FafbPackManifest

        Public Sub New(packFile As String)
            If String.IsNullOrWhiteSpace(packFile) Then Throw New ArgumentNullException(NameOf(packFile))
            If Not File.Exists(packFile) Then Throw New FileNotFoundException($"msgpack 转储包不存在: {packFile}", packFile)

            m_zip = ZipFile.OpenRead(packFile)

            Dim entry As ZipArchiveEntry = m_zip.GetEntry(FafbMsgPackStorage.ManifestEntry)

            If entry Is Nothing Then
                Call Dispose()

                Throw New InvalidDataException($"转储包里没有清单项 {FafbMsgPackStorage.ManifestEntry}: {packFile}")
            End If

            Using stream As Stream = entry.Open()
                Manifest = MsgPackSerializer.Deserialize(Of FafbPackManifest)(stream)
            End Using
        End Sub

        ''' <summary>包里是否有这一个键。</summary>
        Public Function Contains(key As String) As Boolean
            Return m_zip.GetEntry(FafbMsgPackStorage.EntryNameOf(key)) IsNot Nothing
        End Function

        ''' <summary>读回某张表；键不存在时返回 Nothing。</summary>
        Public Function Read(Of T As Class)(key As String) As T
            Dim entry As ZipArchiveEntry = m_zip.GetEntry(FafbMsgPackStorage.EntryNameOf(key))

            If entry Is Nothing Then Return Nothing

            Return FafbMsgPackStorage.Deserialize(Of T)(entry.Open(), True)
        End Function

        ''' <summary>读回一列 Double (快通道，见 <see cref="MsgPackArrayCodec"/>)。</summary>
        Public Function ReadDoubles(key As String) As Double()
            Dim entry As ZipArchiveEntry = m_zip.GetEntry(FafbMsgPackStorage.EntryNameOf(key))

            If entry Is Nothing Then Return Nothing

            Using stream As Stream = entry.Open()
                Return MsgPackArrayCodec.ReadDoubles(stream)
            End Using
        End Function

        ''' <summary>读回一列 Long (快通道)。</summary>
        Public Function ReadLongs(key As String) As Long()
            Dim entry As ZipArchiveEntry = m_zip.GetEntry(FafbMsgPackStorage.EntryNameOf(key))

            If entry Is Nothing Then Return Nothing

            Using stream As Stream = entry.Open()
                Return MsgPackArrayCodec.ReadLongs(stream)
            End Using
        End Function

        ''' <summary>读回一列 Integer (快通道)。</summary>
        Public Function ReadIntegers(key As String) As Integer()
            Dim entry As ZipArchiveEntry = m_zip.GetEntry(FafbMsgPackStorage.EntryNameOf(key))

            If entry Is Nothing Then Return Nothing

            Using stream As Stream = entry.Open()
                Return MsgPackArrayCodec.ReadIntegers(stream)
            End Using
        End Function

        Protected Overridable Sub Dispose(disposing As Boolean)
            If m_disposed Then Return

            If disposing AndAlso m_zip IsNot Nothing Then
                Call m_zip.Dispose()
            End If

            m_disposed = True
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Call Dispose(True)
            Call GC.SuppressFinalize(Me)
        End Sub

    End Class

    ''' <summary>
    ''' FAFB v783 数据源的 <b>msgpack 转储存储过程</b>：
    ''' 把每个 csv 分别转储成一个 msgpack 文件，再一起打进一个 zip 包。
    ''' </summary>
    ''' <remarks>
    ''' <b>为什么要这一层</b>：界面启动时要读 261 MB 的连接表、85 MB 的脑区表等一堆 ASCII 文本，
    ''' 光是 <c>Split(",")</c> + <c>Double.TryParse</c> 就要几十秒，而且每次启动都要重做一遍。
    ''' 转储包把"文本 → 定型数值"这一步只做一次，之后直接从 msgpack 读定型数组。
    ''' 
    ''' <b>包的结构</b>（zip 内每一项都是一份独立、完整的 msgpack 文档）：
    ''' <code>
    '''   manifest.msgpack       清单：格式版本 + 每个源文件的指纹
    '''   names.msgpack          CellNamesPack
    '''   classification.msgpack ClassificationPack
    '''   cell_types.msgpack     CellTypesPack
    '''   neurons.msgpack        NeuronsPack
    '''   coordinates.msgpack    CoordinatesPack
    '''   neuropil.msgpack       NeuropilTablePack   (321 列)
    '''   connections.msgpack    ConnectionsPack     (534 万行)
    ''' </code>
    ''' 
    ''' <b>不是"缓存一切"</b>：转储包只是源 csv 的另一种表示，具体语义（坐标取平均、
    ''' 脑区取最大值、连接按索引过滤）仍然由使用方决定；源 csv 变了，包必须作废重转
    ''' （见 <see cref="Verify"/>）。
    ''' </remarks>
    Public Class FafbMsgPackStorage

        ''' <summary>转储格式版本：结构一变就必须 +1（旧包会被 <see cref="Verify"/> 判为过期）。</summary>
        Public Const FormatVersion As Integer = 1

        ''' <summary>zip 内每一项的扩展名。</summary>
        Public Const EntryExtension As String = ".msgpack"

        ''' <summary>清单项的名字。</summary>
        Public Const ManifestEntry As String = "manifest" & EntryExtension

        ''' <summary>默认转储包文件名（放在数据目录下）。</summary>
        Public Const DefaultPackName As String = "fafb-v783.msgpack.zip"

#Region "zip 内的键"

        Public Const KeyNames As String = "names"
        Public Const KeyClassification As String = "classification"
        Public Const KeyCellTypes As String = "cell_types"
        Public Const KeyNeurons As String = "neurons"
        Public Const KeyCoordinates As String = "coordinates"
        Public Const KeyNeuropil As String = "neuropil"
        Public Const KeyConnections As String = "connections"

        ''' <summary>键 → zip 内的项名。</summary>
        Public Shared Function EntryNameOf(key As String) As String
            Return key & EntryExtension
        End Function

        ''' <summary>
        ''' 脑区表第 <paramref name="column"/> 列的键名。
        ''' </summary>
        ''' <remarks>
        ''' 321 列各自独立成项：用方只需要其中的一部分列（脑区突触数），
        ''' 独立成项之后就不必把整张表读进来。
        ''' </remarks>
        Public Shared Function NeuropilColumnKey(column As Integer) As String
            Return $"{KeyNeuropil}.column.{column}"
        End Function

        ''' <summary>连接表 5 个数据列的列名。</summary>
        Public Const ColumnPre As String = "pre"
        Public Const ColumnPost As String = "post"
        Public Const ColumnNeuropil As String = "neuropil"
        Public Const ColumnSynapses As String = "synapses"
        Public Const ColumnNt As String = "nt"

        ''' <summary>
        ''' 连接表某一列的键名（各自独立成项，走 <see cref="MsgPackArrayCodec"/> 快通道）。
        ''' </summary>
        Public Shared Function ConnectionColumnKey(column As String) As String
            Return $"{KeyConnections}.column.{column}"
        End Function

#End Region

        ''' <summary>
        ''' 转储包的路径：显式给了就用它，否则放在数据目录下 (<see cref="DefaultPackName"/>)。
        ''' </summary>
        Public Shared Function ResolvePackFile(config As SnnConfig, Optional packFile As String = Nothing) As String
            If config Is Nothing Then Throw New ArgumentNullException(NameOf(config))

            If Not String.IsNullOrWhiteSpace(packFile) Then Return packFile

            Return Path.Combine(config.DataDir, DefaultPackName)
        End Function

#Region "转储"

        ''' <summary>
        ''' 把数据目录下的各个 csv <b>分别</b>转储成 msgpack，并打包进一个 zip。
        ''' </summary>
        ''' <param name="config">数据配置（提供 <see cref="SnnConfig.DataDir"/> 与各表名）</param>
        ''' <param name="packFile">输出的 zip 路径；为空时写到数据目录下</param>
        ''' <param name="progress">进度回调（已经格式化的文本）</param>
        ''' <returns>写进包里的清单</returns>
        Public Shared Function Dump(config As SnnConfig,
                                    Optional packFile As String = Nothing,
                                    Optional progress As Action(Of String) = Nothing) As FafbPackManifest

            If config Is Nothing Then Throw New ArgumentNullException(NameOf(config))

            Dim target As String = ResolvePackFile(config, packFile)
            Dim clock As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            Dim sources As New List(Of FafbPackSource)()
            Dim folder As String = Path.GetDirectoryName(Path.GetFullPath(target))

            If Not String.IsNullOrEmpty(folder) AndAlso Not IO.Directory.Exists(folder) Then
                Call IO.Directory.CreateDirectory(folder)
            End If

            Call report(progress, $"dumping FAFB v783 into {target} ...")

            Using archive As ZipArchive = Compression.ZipFile.Open(target, ZipArchiveMode.Create)
                Call sources.Add(dumpTable(archive, config, KeyNames, config.NamesCsv, progress,
                                           Function(path) CellNamesPack.FromRecords(path.LoadCellNames())))
                Call sources.Add(dumpTable(archive, config, KeyClassification, config.ClassificationCsv, progress,
                                           Function(path) ClassificationPack.FromRecords(path.LoadClassification())))
                Call sources.Add(dumpTable(archive, config, KeyCellTypes, config.CellTypesCsv, progress,
                                           Function(path) CellTypesPack.FromRecords(path.LoadCellTypes())))
                Call sources.Add(dumpTable(archive, config, KeyNeurons, config.NeuronsCsv, progress,
                                           Function(path) NeuronsPack.FromRecords(path.LoadNeurons())))
                Call sources.Add(dumpCoordinates(archive, config, progress))
                Call sources.Add(dumpNeuropil(archive, config, progress))
                Call sources.Add(dumpConnections(archive, config, progress))

                ' 清单最后写：它记录的是"前面每一张表都成功转储"这个事实
                Dim manifest As New FafbPackManifest With {
                    .FormatVersion = FormatVersion,
                    .DataDir = config.DataDir,
                    .CreatedUtc = DateTime.UtcNow.ToString("o"),
                    .ElapsedMs = clock.ElapsedMilliseconds,
                    .Sources = sources.ToArray()
                }

                Call write(archive, ManifestEntry, manifest)
            End Using

            clock.Stop()

            Call report(progress, $"msgpack archive ready: {target} ({clock.ElapsedMilliseconds} ms)")

            Return New FafbPackManifest With {
                .FormatVersion = FormatVersion,
                .DataDir = config.DataDir,
                .CreatedUtc = DateTime.UtcNow.ToString("o"),
                .ElapsedMs = clock.ElapsedMilliseconds,
                .Sources = sources.ToArray()
            }
        End Function

        ''' <summary>通用：读一张小表 → 转成列式 → 写进包里。</summary>
        Private Shared Function dumpTable(archive As ZipArchive,
                                          config As SnnConfig,
                                          key As String,
                                          fileName As String,
                                          progress As Action(Of String),
                                          convert As Func(Of String, Object)) As FafbPackSource

            Dim path As String = config.ResolvePath(fileName)
            Dim clock As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()

            If Not File.Exists(path) Then
                Throw New FileNotFoundException($"数据源文件不存在: {path}", path)
            End If

            Call report(progress, $"  [{key}] {fileName} ...")

            Dim pack As Object = convert(path)
            Dim rows As Integer = rowCountOf(pack)

            Call write(archive, EntryNameOf(key), pack)
            clock.Stop()

            Call report(progress, $"  [{key}] {rows:N0} rows, {clock.ElapsedMilliseconds} ms")

            Return fingerprint(config, key, fileName, rows, clock.ElapsedMilliseconds)
        End Function

        ''' <summary>``coordinates.csv``：流式读 (23 万行，仍然走 Stream* 以免一次吃掉全部对象)。</summary>
        Private Shared Function dumpCoordinates(archive As ZipArchive,
                                                config As SnnConfig,
                                                progress As Action(Of String)) As FafbPackSource

            Dim path As String = config.ResolvePath(config.CoordinatesCsv)
            Dim clock As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            Dim rootIds As New List(Of Long)()
            Dim positions As New List(Of String)()
            Dim supervoxels As New List(Of Long)()

            Call report(progress, $"  [{KeyCoordinates}] {config.CoordinatesCsv} ...")

            For Each cell As Coordinates In path.StreamCoordinates()
                If cell Is Nothing Then Continue For

                Call rootIds.Add(cell.RootId)
                Call positions.Add(cell.Position)
                Call supervoxels.Add(cell.SupervoxelId)
            Next

            Dim pack As New CoordinatesPack With {
                .RootId = rootIds.ToArray(),
                .Position = positions.ToArray(),
                .SupervoxelId = supervoxels.ToArray()
            }

            Call write(archive, EntryNameOf(KeyCoordinates), pack)
            clock.Stop()

            Call report(progress, $"  [{KeyCoordinates}] {pack.RootId.Length:N0} rows, {clock.ElapsedMilliseconds} ms")

            Return fingerprint(config, KeyCoordinates, config.CoordinatesCsv, pack.RootId.Length, clock.ElapsedMilliseconds)
        End Function

        ''' <summary>
        ''' ``neuropil_synapse_table.csv``：321 列，按列存储。
        ''' </summary>
        ''' <remarks>
        ''' 先空跑一遍数出总行数，再按行数一次性分配 321 条定长数组
        ''' （用 <c>List(Of Double)</c> 的话 321 × 13 万在扩容时会瞬时翻倍到 700 MB）。
        ''' </remarks>
        Private Shared Function dumpNeuropil(archive As ZipArchive,
                                             config As SnnConfig,
                                             progress As Action(Of String)) As FafbPackSource

            Dim path As String = config.ResolvePath(config.NeuropilTableCsv)
            Dim clock As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            Dim header As String() = Nothing
            Dim rows As Integer = 0

            Call report(progress, $"  [{KeyNeuropil}] {config.NeuropilTableCsv} (counting rows) ...")

            For Each line As String In File.ReadLines(path)
                If header Is Nothing Then
                    header = line.Split(","c)
                Else
                    rows += 1
                End If
            Next

            If header Is Nothing OrElse header.Length = 0 Then
                Throw New InvalidDataException($"脑区表是空的或者没有表头: {path}")
            End If

            Dim columns As Integer = header.Length
            Dim values As Double()() = New Double(columns - 1)() {}

            For c As Integer = 0 To columns - 1
                values(c) = New Double(Math.Max(0, rows - 1)) {}
            Next

            Dim rootIds As Long() = New Long(Math.Max(0, rows - 1)) {}
            Dim row As Integer = 0
            Dim firstLine As Boolean = True

            For Each line As String In File.ReadLines(path)
                If firstLine Then
                    firstLine = False

                    Continue For
                End If

                If row >= rows Then Exit For

                Dim parts As String() = line.Split(","c)
                Dim i As Integer = row

                row += 1

                If i < rootIds.Length Then
                    Dim rootId As Long

                    If Long.TryParse(If(parts.Length > 0, parts(0), ""), NumberStyles.Integer, CultureInfo.InvariantCulture, rootId) Then
                        rootIds(i) = rootId
                    End If
                End If

                Dim limit As Integer = Math.Min(parts.Length, columns)

                For c As Integer = 0 To limit - 1
                    Dim value As Double

                    If Double.TryParse(parts(c), NumberStyles.Float, CultureInfo.InvariantCulture, value) Then
                        values(c)(i) = value
                    End If
                Next

                If (row Mod 20000) = 0 Then
                    Call report(progress, $"  [{KeyNeuropil}] {row:N0} rows ...")
                End If
            Next

            Dim pack As New NeuropilTablePack With {
                .RootId = rootIds,
                .Columns = header,
                .ColumnCount = columns
            }

            Call write(archive, EntryNameOf(KeyNeuropil), pack)

            ' 每一列单独写一项：用方只读它需要的那几列（数值列走快通道）
            For c As Integer = 0 To columns - 1
                Call writeDoubles(archive, NeuropilColumnKey(c), values(c))
            Next

            clock.Stop()

            Call report(progress, $"  [{KeyNeuropil}] {rows:N0} rows x {columns} columns, {clock.ElapsedMilliseconds} ms")

            Return fingerprint(config, KeyNeuropil, config.NeuropilTableCsv, rows, clock.ElapsedMilliseconds)
        End Function

        ''' <summary>
        ''' ``connections_princeton.csv``：534 万行，按位置切分 + 字典化两个分类列。
        ''' </summary>
        ''' <remarks>
        ''' 不走 <c>StreamConnections</c>：那是按属性名反射映射，534 万行实测要几十秒；
        ''' 这里与界面加载器一样按字段位置取值（该表的字段都是整数与短标识符，没有引号包裹的逗号）。
        ''' 字段顺序：``pre_root_id, post_root_id, neuropil, syn_count, nt_type``。
        ''' </remarks>
        Private Shared Function dumpConnections(archive As ZipArchive,
                                                config As SnnConfig,
                                                progress As Action(Of String)) As FafbPackSource

            Dim path As String = config.ResolvePath(config.ConnectionsCsv)
            Dim clock As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            Dim capacity As Integer = 5400000

            Dim pre As New List(Of Long)(capacity)
            Dim post As New List(Of Long)(capacity)
            Dim synapses As New List(Of Double)(capacity)
            Dim neuropilCodes As New List(Of Integer)(capacity)
            Dim ntCodes As New List(Of Integer)(capacity)

            Dim neuropilNames As New List(Of String)()
            Dim neuropilIds As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
            Dim ntNames As New List(Of String)()
            Dim ntIds As New Dictionary(Of String, Integer)(StringComparer.Ordinal)

            Dim rows As Long = 0
            Dim firstLine As Boolean = True

            Call report(progress, $"  [{KeyConnections}] {config.ConnectionsCsv} ...")

            For Each line As String In File.ReadLines(path)
                If firstLine Then
                    firstLine = False

                    Continue For
                End If

                If line.Length = 0 Then Continue For

                rows += 1

                If (rows Mod 500000) = 0 Then
                    Call report(progress, $"  [{KeyConnections}] {rows:N0} rows ...")
                End If

                Dim parts As String() = line.Split(","c)

                If parts.Length < 5 Then Continue For

                Dim preRoot As Long
                Dim postRoot As Long

                If Not Long.TryParse(parts(0), NumberStyles.Integer, CultureInfo.InvariantCulture, preRoot) Then Continue For
                If Not Long.TryParse(parts(1), NumberStyles.Integer, CultureInfo.InvariantCulture, postRoot) Then Continue For

                Dim synCount As Double

                If Not Double.TryParse(parts(3), NumberStyles.Float, CultureInfo.InvariantCulture, synCount) Then
                    synCount = 0
                End If

                Call pre.Add(preRoot)
                Call post.Add(postRoot)
                Call synapses.Add(synCount)
                Call neuropilCodes.Add(intern(parts(2), neuropilNames, neuropilIds))
                Call ntCodes.Add(intern(parts(4), ntNames, ntIds))
            Next

            Dim pack As New ConnectionsPack With {
                .RowCount = pre.Count,
                .NeuropilNames = neuropilNames.ToArray(),
                .NtNames = ntNames.ToArray()
            }

            Call write(archive, EntryNameOf(KeyConnections), pack)
            Call writeLongs(archive, ConnectionColumnKey(ColumnPre), pre.ToArray())
            Call writeLongs(archive, ConnectionColumnKey(ColumnPost), post.ToArray())
            Call writeIntegers(archive, ConnectionColumnKey(ColumnNeuropil), neuropilCodes.ToArray())
            Call writeDoubles(archive, ConnectionColumnKey(ColumnSynapses), synapses.ToArray())
            Call writeIntegers(archive, ConnectionColumnKey(ColumnNt), ntCodes.ToArray())

            clock.Stop()

            Call report(progress, $"  [{KeyConnections}] {pack.RowCount:N0} rows " &
                                  $"({neuropilNames.Count} neuropils, {ntNames.Count} neurotransmitters), {clock.ElapsedMilliseconds} ms")

            Return fingerprint(config, KeyConnections, config.ConnectionsCsv, pack.RowCount, clock.ElapsedMilliseconds)
        End Function

#End Region

#Region "读取与校验"

        ''' <summary>
        ''' 反序列化一个 msgpack 项。
        ''' </summary>
        ''' <remarks>
        ''' 先把这一项<b>完整解压到内存</b>再交给 msgpack 的读取器：读取器是逐字节读的
        ''' (每个值都要先读一个格式头字节)，直接压在 <c>DeflateStream</c> 上时每一次
        ''' <c>ReadByte</c> 都要走一遍解压管线 —— 534 万行的连接表实测差了 2 倍以上。
        ''' </remarks>
        Friend Shared Function Deserialize(Of T As Class)(stream As Stream, Optional disposeStream As Boolean = False) As T
            Try
                Using buffer As New MemoryStream()
                    Call stream.CopyTo(buffer)
                    Call buffer.Flush()

                    buffer.Position = 0

                    Return MsgPackSerializer.Deserialize(Of T)(buffer)
                End Using
            Finally
                If disposeStream AndAlso stream IsNot Nothing Then
                    Call stream.Dispose()
                End If
            End Try
        End Function

        ''' <summary>打开一个转储包。</summary>
        Public Shared Function Open(packFile As String) As FafbPackReader
            Return New FafbPackReader(packFile)
        End Function

        ''' <summary>
        ''' 检查转储包能不能用：返回空串表示"新鲜可用"，否则返回原因。
        ''' </summary>
        ''' <remarks>
        ''' 源 csv 被替换/更新之后必须重新转储，否则界面读到的是一份过期的缓存。
        ''' </remarks>
        Public Shared Function Verify(config As SnnConfig, Optional packFile As String = Nothing) As String
            Dim target As String = ResolvePackFile(config, packFile)

            If Not File.Exists(target) Then Return $"转储包不存在: {target}"

            Dim manifest As FafbPackManifest

            Try
                Using reader As New FafbPackReader(target)
                    manifest = reader.Manifest
                End Using
            Catch ex As Exception
                Return $"转储包无法读取: {ex.Message}"
            End Try

            If manifest Is Nothing Then Return "转储包里没有清单"
            If manifest.FormatVersion <> FormatVersion Then
                Return $"转储包版本 {manifest.FormatVersion} 与当前版本 {FormatVersion} 不一致（需要重新转储）"
            End If

            If manifest.Sources Is Nothing OrElse manifest.Sources.Length = 0 Then Return "转储包清单里没有源文件记录"

            For Each source As FafbPackSource In manifest.Sources
                Dim path As String = config.ResolvePath(source.FileName)

                If Not File.Exists(path) Then Return $"源文件已经不在了: {path}"
                If New FileInfo(path).Length <> source.Length Then Return $"源文件大小变了: {source.FileName}"
                If File.GetLastWriteTimeUtc(path).Ticks <> source.LastWriteTicks Then Return $"源文件被改过: {source.FileName}"
            Next

            Return ""
        End Function

        ''' <summary>转储包是否可用（<see cref="Verify"/> 的空串判定）。</summary>
        Public Shared Function IsUsable(config As SnnConfig, Optional packFile As String = Nothing) As Boolean
            Return String.IsNullOrEmpty(Verify(config, packFile))
        End Function

#End Region

#Region "内部"

        ''' <summary>把一份对象写成包里的一个 msgpack 项。</summary>
        Private Shared Sub write(archive As ZipArchive, entryName As String, pack As Object)
            Using stream As Stream = archive.CreateEntry(entryName, CompressionLevel.Optimal).Open()
                Call MsgPackSerializer.SerializeObject(pack, stream)
            End Using
        End Sub

        ''' <summary>把一列数值写成包里的一个 msgpack 项（快通道：整段写，不做逐元素反射）。</summary>
        Private Shared Sub writeDoubles(archive As ZipArchive, key As String, values As Double())
            Using stream As Stream = archive.CreateEntry(EntryNameOf(key), CompressionLevel.Optimal).Open()
                Call MsgPackArrayCodec.WriteDoubles(stream, values)
            End Using
        End Sub

        Private Shared Sub writeLongs(archive As ZipArchive, key As String, values As Long())
            Using stream As Stream = archive.CreateEntry(EntryNameOf(key), CompressionLevel.Optimal).Open()
                Call MsgPackArrayCodec.WriteLongs(stream, values)
            End Using
        End Sub

        Private Shared Sub writeIntegers(archive As ZipArchive, key As String, values As Integer())
            Using stream As Stream = archive.CreateEntry(EntryNameOf(key), CompressionLevel.Optimal).Open()
                Call MsgPackArrayCodec.WriteIntegers(stream, values)
            End Using
        End Sub

        ''' <summary>源文件指纹。</summary>
        Private Shared Function fingerprint(config As SnnConfig, key As String, fileName As String,
                                            rows As Integer, elapsed As Long) As FafbPackSource
            Dim path As String = config.ResolvePath(fileName)
            Dim info As New FileInfo(path)

            Return New FafbPackSource With {
                .Key = key,
                .FileName = fileName,
                .Length = info.Length,
                .LastWriteTicks = info.LastWriteTimeUtc.Ticks,
                .Rows = rows,
                .ElapsedMs = elapsed
            }
        End Function

        ''' <summary>列式表的行数（各表第一列都是 RootId / Pre）。</summary>
        Private Shared Function rowCountOf(pack As Object) As Integer
            Select Case True
                Case TypeOf pack Is CellNamesPack
                    Return DirectCast(pack, CellNamesPack).RootId.Length
                Case TypeOf pack Is ClassificationPack
                    Return DirectCast(pack, ClassificationPack).RootId.Length
                Case TypeOf pack Is CellTypesPack
                    Return DirectCast(pack, CellTypesPack).RootId.Length
                Case TypeOf pack Is NeuronsPack
                    Return DirectCast(pack, NeuronsPack).RootId.Length
                Case Else
                    Return 0
            End Select
        End Function

        ''' <summary>把分类文本映射到连续编号（保持首次出现的顺序）。</summary>
        Private Shared Function intern(name As String, names As List(Of String), ids As Dictionary(Of String, Integer)) As Integer
            Dim text As String = If(String.IsNullOrWhiteSpace(name), "(unknown)", name.Trim())
            Dim id As Integer

            If ids.TryGetValue(text, id) Then Return id

            id = names.Count

            Call names.Add(text)
            Call ids.Add(text, id)

            Return id
        End Function

        Private Shared Sub report(progress As Action(Of String), message As String)
            If progress Is Nothing Then Return

            Call progress(message)
        End Sub

#End Region

    End Class

End Namespace
