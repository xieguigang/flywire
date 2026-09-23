Imports System.Runtime.CompilerServices

Namespace FAFBv783

    ''' <summary>
    ''' 骨架模型 (<see cref="SwcSkeleton"/>) 与 FAFB v783 的其它数据表格模型之间的互操作帮助函数：
    ''' 按照 FlyWire 的 ``root_id`` 进行关联与 join 查询。
    ''' 
    ''' 既有数据模型 (``CellTypes`` / ``CellNames`` / ``Classification`` / ``CellStats`` 等) 之中的
    ''' ``RootId`` 属性与骨架的 <see cref="SwcSkeleton.RootId"/> 是同一个数据键。
    ''' </summary>
    Public Module SkeletonInterops

        ''' <summary>
        ''' 骨架在归档之中的条目名。
        ''' </summary>
        <Extension>
        Public Function SkeletonEntryName(rootId As Long) As String
            Return $"{rootId}.swc"
        End Function

        ''' <summary>
        ''' 从骨架归档之中读取目标 root_id 的骨架 (归档之中不存在的时候返回 Nothing)。
        ''' </summary>
        <Extension>
        Public Function GetSkeleton(archive As SkeletonArchive, rootId As Long) As SwcSkeleton
            If archive Is Nothing Then
                Return Nothing
            Else
                Return archive.ReadSkeleton(rootId)
            End If
        End Function

        ''' <summary>
        ''' 读取细胞名称注释所对应的骨架。
        ''' </summary>
        <Extension>
        Public Function GetSkeleton(archive As SkeletonArchive, cell As CellNames) As SwcSkeleton
            If cell Is Nothing Then
                Return Nothing
            Else
                Return archive.GetSkeleton(cell.RootId)
            End If
        End Function

        ''' <summary>
        ''' 读取细胞类型注释所对应的骨架。
        ''' </summary>
        <Extension>
        Public Function GetSkeleton(archive As SkeletonArchive, cell As CellTypes) As SwcSkeleton
            If cell Is Nothing Then
                Return Nothing
            Else
                Return archive.GetSkeleton(cell.RootId)
            End If
        End Function

        ''' <summary>
        ''' 读取层次化分类注释所对应的骨架。
        ''' </summary>
        <Extension>
        Public Function GetSkeleton(archive As SkeletonArchive, cell As Classification) As SwcSkeleton
            If cell Is Nothing Then
                Return Nothing
            Else
                Return archive.GetSkeleton(cell.RootId)
            End If
        End Function

        ''' <summary>
        ''' 读取细胞尺寸测量数据所对应的骨架。
        ''' </summary>
        <Extension>
        Public Function GetSkeleton(archive As SkeletonArchive, cell As CellStats) As SwcSkeleton
            If cell Is Nothing Then
                Return Nothing
            Else
                Return archive.GetSkeleton(cell.RootId)
            End If
        End Function

        ''' <summary>
        ''' 按照 root_id 建立数据行的索引 (同一个 root_id 出现多次的时候保留第一条数据行)。
        ''' </summary>
        <Extension>
        Public Function ToRootIdIndex(Of T)(source As IEnumerable(Of T), rootId As Func(Of T, Long)) As Dictionary(Of Long, T)
            Dim index As New Dictionary(Of Long, T)

            If source Is Nothing Then
                Return index
            End If

            For Each item As T In source
                If item Is Nothing Then
                    Continue For
                End If

                Dim id As Long = rootId(item)

                If id > 0 AndAlso Not index.ContainsKey(id) Then
                    Call index.Add(id, item)
                End If
            Next

            Return index
        End Function

        ''' <summary>
        ''' 将数据行按照 root_id 与骨架进行 join。
        ''' </summary>
        ''' <param name="source">数据行集合，例如 <see cref="CellNames"/> 或者 <see cref="CellTypes"/>。</param>
        ''' <param name="rootId">从数据行之中读取 root_id 的函数。</param>
        ''' <param name="skeletons">骨架索引，请通过
        ''' <see cref="FAFBv783SkeletonHelper.ToSkeletonIndex(SkeletonArchive, IEnumerable(Of Long))"/> 建立。</param>
        <Extension>
        Public Iterator Function JoinSkeleton(Of T)(source As IEnumerable(Of T),
                                                    rootId As Func(Of T, Long),
                                                    skeletons As IReadOnlyDictionary(Of Long, SwcSkeleton)) As IEnumerable(Of (cell As T, skeleton As SwcSkeleton))

            If source Is Nothing Then
                Return
            End If

            For Each item As T In source
                Dim id As Long = rootId(item)
                Dim skeleton As SwcSkeleton = Nothing

                If Not skeletons Is Nothing Then
                    Call skeletons.TryGetValue(id, skeleton)
                End If

                Yield (item, skeleton)
            Next
        End Function

        ''' <summary>
        ''' 骨架的单行摘要文本，便于在控制台输出与日志之中查看。
        ''' </summary>
        <Extension>
        Public Function Describe(skeleton As SwcSkeleton) As String
            If skeleton Is Nothing Then
                Return "<no skeleton>"
            End If

            Return $"{skeleton.EntryName}: {skeleton.Count} nodes, " &
                $"fork={skeleton.ForkPoints.Length}, end={skeleton.EndPoints.Length}, " &
                $"cable={Math.Round(skeleton.CableLengthMicrons, 3)} um"
        End Function

    End Module

    ''' <summary>
    ''' 骨架索引的构建帮助函数。
    ''' </summary>
    Public Module FAFBv783SkeletonHelper

        ''' <summary>
        ''' 读取指定的一组 root_id 的骨架并且建立查询索引。
        ''' </summary>
        ''' <param name="archive">骨架归档。</param>
        ''' <param name="rootIds">需要读取的 root_id 集合。</param>
        <Extension>
        Public Function ToSkeletonIndex(archive As SkeletonArchive, rootIds As IEnumerable(Of Long)) As Dictionary(Of Long, SwcSkeleton)
            Dim index As New Dictionary(Of Long, SwcSkeleton)

            If archive Is Nothing OrElse rootIds Is Nothing Then
                Return index
            End If

            For Each skeleton As SwcSkeleton In archive.EnumerateSkeletons(rootIds)
                If Not index.ContainsKey(skeleton.RootId) Then
                    Call index.Add(skeleton.RootId, skeleton)
                End If
            Next

            Return index
        End Function

        ''' <summary>
        ''' 以惰性迭代的方式读取归档之中的全部骨架并且建立查询索引。
        ''' 
        ''' (注意：全量的枚举需要解压 13 GB 的归档内容)
        ''' </summary>
        <Extension>
        Public Function ToSkeletonIndex(archive As SkeletonArchive, Optional limit As Integer = -1) As Dictionary(Of Long, SwcSkeleton)
            Dim index As New Dictionary(Of Long, SwcSkeleton)

            If archive Is Nothing Then
                Return index
            End If

            For Each skeleton As SwcSkeleton In archive.EnumerateSkeletons(limit)
                If Not index.ContainsKey(skeleton.RootId) Then
                    Call index.Add(skeleton.RootId, skeleton)
                End If
            Next

            Return index
        End Function

    End Module
End Namespace
