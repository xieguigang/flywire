Imports System.IO
Imports System.IO.Compression

Namespace FAFBv783

    ''' <summary>
    ''' ``sk_lod1_783_healed.zip`` 神经元骨架归档文件的读取封装。
    ''' 
    ''' 归档之中包含 13 万个 ``&lt;root_id&gt;.swc`` 文本文件，每一个文件都是一个神经元的
    ''' SWC 骨架 (解析器参考 <see cref="SwcParser"/>)。
    ''' </summary>
    ''' <remarks>
    ''' * **单个读取**：<see cref="ReadSkeleton(Long)"/> 与 <see cref="ReadSkeleton(String)"/>；
    ''' * **批量分析**：<see cref="EnumerateSkeletons(Integer)"/> 与
    '''   <see cref="EnumerateSkeletons(IEnumerable(Of Long))"/> 都是惰性的迭代器，逐个条目
    '''   解压并解析，内存占用为 ``O(1)``，不会一次性展开 13 GB 的归档内容；
    ''' * 本类型持有一个打开的 <see cref="ZipArchive"/>，使用完毕之后需要调用 <see cref="Dispose"/>。
    ''' </remarks>
    Public Class SkeletonArchive : Implements IDisposable

        ''' <summary>
        ''' 归档的名字 -> 归档条目 的索引 (首次访问的时候才会建立)。
        ''' </summary>
        ReadOnly _entries As Dictionary(Of String, ZipArchiveEntry) = New Dictionary(Of String, ZipArchiveEntry)(StringComparer.OrdinalIgnoreCase)

        ''' <summary>
        ''' 骨架的 root_id -> 归档条目 的索引 (首次访问的时候才会建立)。
        ''' </summary>
        ReadOnly _byRootId As Dictionary(Of Long, ZipArchiveEntry) = New Dictionary(Of Long, ZipArchiveEntry)()

        ReadOnly _zip As ZipArchive
        Dim _indexed As Boolean

        ''' <summary>
        ''' 归档文件的文件路径。
        ''' </summary>
        Public ReadOnly Property Path As String

        ''' <summary>
        ''' 归档之中的条目总数。
        ''' </summary>
        Public ReadOnly Property EntryCount As Integer
            Get
                Return _zip.Entries.Count
            End Get
        End Property

        ''' <summary>
        ''' 打开指定的骨架归档文件。
        ''' </summary>
        ''' <param name="path">``*.zip`` 归档文件的文件路径。</param>
        Public Sub New(path As String)
            Me.Path = path
            Me._zip = ZipFile.OpenRead(path)
        End Sub

        ''' <summary>
        ''' 打开指定的骨架归档文件。
        ''' </summary>
        Public Shared Function Open(path As String) As SkeletonArchive
            Return New SkeletonArchive(path)
        End Function

        ''' <summary>
        ''' 以惰性迭代的方式枚举归档之中的全部条目名 (例如 ``720575940590515268.swc``)。
        ''' </summary>
        Public Iterator Function EntryNames() As IEnumerable(Of String)
            For Each entry As ZipArchiveEntry In _zip.Entries
                Yield entry.Name
            Next
        End Function

        ''' <summary>
        ''' 以惰性迭代的方式枚举归档之中全部骨架的 root_id (从条目名解析得到)。
        ''' </summary>
        Public Iterator Function RootIds() As IEnumerable(Of Long)
            For Each entry As ZipArchiveEntry In _zip.Entries
                Dim rootId As Long

                If tryParseRootId(entry.Name, rootId) Then
                    Yield rootId
                End If
            Next
        End Function

        ''' <summary>
        ''' 归档之中是否存在目标 root_id 的骨架？
        ''' </summary>
        Public Function Contains(rootId As Long) As Boolean
            Call ensureIndex()

            Return _byRootId.ContainsKey(rootId)
        End Function

        ''' <summary>
        ''' 归档之中是否存在目标条目名的骨架？(条目名缺省的扩展名会被自动补全)
        ''' </summary>
        Public Function Contains(entryName As String) As Boolean
            Call ensureIndex()

            Return _entries.ContainsKey(normalizeEntryName(entryName))
        End Function

        ''' <summary>
        ''' 读取指定 root_id 的骨架；归档之中没有对应骨架的时候返回 Nothing。
        ''' </summary>
        Public Function ReadSkeleton(rootId As Long) As SwcSkeleton
            Call ensureIndex()

            Dim entry As ZipArchiveEntry = Nothing

            If _byRootId.TryGetValue(rootId, entry) Then
                Return readSkeleton(entry)
            Else
                Return Nothing
            End If
        End Function

        ''' <summary>
        ''' 读取指定条目名的骨架；归档之中没有对应条目的时候返回 Nothing。
        ''' </summary>
        ''' <param name="entryName">例如 ``720575940590515268.swc`` 或者 ``720575940590515268``。</param>
        Public Function ReadSkeleton(entryName As String) As SwcSkeleton
            Call ensureIndex()

            Dim entry As ZipArchiveEntry = Nothing

            If _entries.TryGetValue(normalizeEntryName(entryName), entry) Then
                Return readSkeleton(entry)
            Else
                Return Nothing
            End If
        End Function

        ''' <summary>
        ''' 打开归档之中的目标条目，返回原始的压缩条目数据流 (调用方负责释放)；
        ''' 归档之中没有对应条目的时候返回 Nothing。
        ''' </summary>
        ''' <param name="entryName">例如 ``720575940590515268.swc`` 或者 ``720575940590515268``。</param>
        Public Function OpenEntry(entryName As String) As Stream
            Call ensureIndex()

            Dim entry As ZipArchiveEntry = Nothing

            If _entries.TryGetValue(normalizeEntryName(entryName), entry) Then
                Return entry.Open()
            Else
                Return Nothing
            End If
        End Function

        ''' <summary>
        ''' 以惰性迭代的方式解析归档之中的骨架 (批量分析用)。
        ''' </summary>
        ''' <param name="limit">
        ''' 最多解析的骨架数量，``-1`` 表示不限制 (注意：全量的枚举需要解压 13 GB 的归档内容)。
        ''' </param>
        Public Iterator Function EnumerateSkeletons(Optional limit As Integer = -1) As IEnumerable(Of SwcSkeleton)
            Dim parsed As Integer = 0

            For Each entry As ZipArchiveEntry In _zip.Entries
                If limit >= 0 AndAlso parsed >= limit Then
                    Exit For
                End If

                Yield readSkeleton(entry)
                parsed += 1
            Next
        End Function

        ''' <summary>
        ''' 以惰性迭代的方式读取指定的一组 root_id 对应的骨架。
        ''' </summary>
        Public Iterator Function EnumerateSkeletons(rootIds As IEnumerable(Of Long)) As IEnumerable(Of SwcSkeleton)
            If rootIds Is Nothing Then
                Return
            End If

            For Each rootId As Long In rootIds
                Dim skeleton As SwcSkeleton = ReadSkeleton(rootId)

                If Not skeleton Is Nothing Then
                    Yield skeleton
                End If
            Next
        End Function

        ''' <summary>
        ''' 打开归档条目并且解析出骨架。
        ''' </summary>
        Private Function readSkeleton(entry As ZipArchiveEntry) As SwcSkeleton
            Using stream As Stream = entry.Open()
                Return SwcParser.Parse(stream, entry.Name)
            End Using
        End Function

        ''' <summary>
        ''' 建立归档条目的索引 (只在首次访问的时候执行一次)。
        ''' </summary>
        Private Sub ensureIndex()
            If _indexed Then
                Return
            End If

            For Each entry As ZipArchiveEntry In _zip.Entries
                If Not _entries.ContainsKey(entry.Name) Then
                    Call _entries.Add(entry.Name, entry)
                End If

                Dim rootId As Long

                If tryParseRootId(entry.Name, rootId) AndAlso Not _byRootId.ContainsKey(rootId) Then
                    Call _byRootId.Add(rootId, entry)
                End If
            Next

            _indexed = True
        End Sub

        ''' <summary>
        ''' 条目名规范化：自动补全 ``.swc`` 扩展名。
        ''' </summary>
        Private Function normalizeEntryName(entryName As String) As String
            If String.IsNullOrWhiteSpace(entryName) Then
                Return entryName
            End If

            If entryName.EndsWith(".swc", StringComparison.OrdinalIgnoreCase) Then
                Return entryName
            Else
                Return entryName & ".swc"
            End If
        End Function

        Private Function tryParseRootId(entryName As String, ByRef rootId As Long) As Boolean
            Dim name As String = System.IO.Path.GetFileNameWithoutExtension(entryName)
            Dim parser As New Int64Parser
            Dim parse As Object = parser.TryParse(name)

            rootId = If(parse Is Nothing, 0L, CLng(parse))

            Return rootId > 0
        End Function

#Region "IDisposable Support"

        Private disposedValue As Boolean

        Protected Overridable Overloads Sub Dispose(disposing As Boolean)
            If Not Me.disposedValue Then
                If disposing Then
                    Call _zip.Dispose()

                    ' 归档索引只保存引用，释放掉以减少内存占用
                    Call _entries.Clear()
                    Call _byRootId.Clear()
                End If
            End If

            Me.disposedValue = True
        End Sub

        Public Overloads Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
        End Sub

#End Region

        Public Overrides Function ToString() As String
            Return $"{Path} ({EntryCount} skeletons)"
        End Function

    End Class
End Namespace
