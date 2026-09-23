Imports System.Linq
Imports FlywireAI.FAFBv783

Namespace Connectome

    ''' <summary>
    ''' 果蝇全脑连接组的神经元索引表：``root_id`` ↔ 连续索引 ``[0, N)`` 的双向映射，
    ''' 以及每个神经元所关联的注释信息 (``name`` / ``group`` / ``class`` / ``primary_type`` / 递质类型)。
    ''' </summary>
    ''' <remarks>
    ''' 构建顺序：
    ''' 
    ''' 1. ``New ConnectomeIndex`` 之后，先用细胞注释表的 root_id 建立主索引 (<see cref="Add"/>)；
    ''' 2. 扫描连接表 (``connections_princeton.csv``) 的时候，对出现的未知 root_id 追加索引
    '''    (<see cref="GetOrAddIndex"/>)，从而保证所有的突触都能落在 ``[0, N)`` 范围之内；
    ''' 3. <see cref="Freeze"/> 冻结索引 (之后不能再追加神经元)；
    ''' 4. <see cref="AttachAnnotations"/> 回填每个神经元的注释数组。
    ''' 
    ''' 索引顺序由插入顺序决定，因此只要输入文件的顺序固定，索引与仿真结果即可复现。
    ''' </remarks>
    Public Class ConnectomeIndex

        ReadOnly _index As New Dictionary(Of Long, Integer)()
        ReadOnly _rootIds As New List(Of Long)()

        Dim _frozen As Boolean

        ' 以下数组的下标与神经元索引一一对应，在 Freeze 的时候分配
        Dim _names As String()
        Dim _groups As String()
        Dim _classes As String()
        Dim _superClasses As String()
        Dim _primaryTypes As String()
        Dim _flows As String()
        Dim _excitatory As Boolean()
        Dim _annotated As Boolean()

        ''' <summary>神经元总数 N。</summary>
        Public ReadOnly Property Size As Integer
            Get
                Return _rootIds.Count
            End Get
        End Property

        ''' <summary>索引是否已经冻结？</summary>
        Public ReadOnly Property Frozen As Boolean
            Get
                Return _frozen
            End Get
        End Property

        ''' <summary>已经关联到注释信息的神经元数量。</summary>
        Public ReadOnly Property AnnotatedCount As Integer
            Get
                If _annotated Is Nothing Then
                    Return 0
                Else
                    Return _annotated.Count(Function(x) x)
                End If
            End Get
        End Property

        ''' <summary>没有关联到任何注释信息的神经元数量。</summary>
        Public ReadOnly Property MissingAnnotationCount As Integer
            Get
                Return Size - AnnotatedCount
            End Get
        End Property

        ''' <summary>
        ''' 添加一个神经元 (已有的 root_id 直接返回其索引)。
        ''' </summary>
        Public Function Add(rootId As Long) As Integer
            If _frozen Then
                Throw New InvalidOperationException("连接组索引已经冻结，不能再追加神经元")
            End If

            Dim index As Integer

            If _index.TryGetValue(rootId, index) Then
                Return index
            End If

            index = _rootIds.Count

            Call _rootIds.Add(rootId)
            Call _index.Add(rootId, index)

            Return index
        End Function

        ''' <summary>
        ''' 取得 root_id 所对应的索引；不存在的时候追加一个新的索引 (扫描连接表时使用)。
        ''' </summary>
        Public Function GetOrAddIndex(rootId As Long) As Integer
            Return Add(rootId)
        End Function

        ''' <summary>
        ''' 查询 root_id 所对应的索引。
        ''' </summary>
        Public Function IndexOf(rootId As Long, ByRef index As Integer) As Boolean
            Return _index.TryGetValue(rootId, index)
        End Function

        ''' <summary>
        ''' 索引 -> root_id。
        ''' </summary>
        Public Function GetRootId(index As Integer) As Long
            If index < 0 OrElse index >= _rootIds.Count Then
                Throw New ArgumentOutOfRangeException(NameOf(index), $"神经元索引 {index} 超出范围 [0, {_rootIds.Count})")
            End If

            Return _rootIds(index)
        End Function

        ''' <summary>
        ''' 全部 root_id (索引顺序)。
        ''' </summary>
        Public Function RootIds() As Long()
            Return _rootIds.ToArray
        End Function

        ''' <summary>
        ''' 冻结索引并且分配注释数组。
        ''' </summary>
        Public Sub Freeze()
            If _frozen Then
                Return
            End If

            _names = New String(Size - 1) {}
            _groups = New String(Size - 1) {}
            _classes = New String(Size - 1) {}
            _superClasses = New String(Size - 1) {}
            _primaryTypes = New String(Size - 1) {}
            _flows = New String(Size - 1) {}
            _excitatory = New Boolean(Size - 1) {}
            _annotated = New Boolean(Size - 1) {}

            ' 没有注释信息的时候默认为兴奋性神经元
            For i As Integer = 0 To _excitatory.Length - 1
                _excitatory(i) = True
            Next

            _frozen = True
        End Sub

        ''' <summary>
        ''' 把细胞注释表格回填到每个神经元之上 (缺注释的神经元保留空串并且不标记为 annotated)。
        ''' </summary>
        ''' <param name="names">``names.csv`` 表格：name / group</param>
        ''' <param name="classification">``classification.csv`` 表格：class / super_class</param>
        ''' <param name="cellTypes">``consolidated_cell_types.csv`` 表格：primary_type</param>
        ''' <param name="neurons">``neurons.csv`` 表格：nt_type (用于判定兴奋性)</param>
        Public Sub AttachAnnotations(names As IEnumerable(Of CellNames),
                                     Optional classification As IEnumerable(Of Classification) = Nothing,
                                     Optional cellTypes As IEnumerable(Of CellTypes) = Nothing,
                                     Optional neurons As IEnumerable(Of Neurons) = Nothing)

            If Not _frozen Then
                Throw New InvalidOperationException("请先调用 Freeze() 冻结索引，再回填注释信息")
            End If

            Dim index As Integer

            If Not names Is Nothing Then
                For Each cell As CellNames In names
                    If cell Is Nothing OrElse Not IndexOf(cell.RootId, index) Then
                        Continue For
                    End If

                    _names(index) = If(cell.Name, "")
                    _groups(index) = If(cell.Group, "")
                    _annotated(index) = True
                Next
            End If

            If Not classification Is Nothing Then
                For Each cell As Classification In classification
                    If cell Is Nothing OrElse Not IndexOf(cell.RootId, index) Then
                        Continue For
                    End If

                    _classes(index) = If(cell.Class, "")
                    _superClasses(index) = If(cell.SuperClass, "")
                    _flows(index) = If(cell.Flow, "")
                    _annotated(index) = True
                Next
            End If

            If Not cellTypes Is Nothing Then
                For Each cell As CellTypes In cellTypes
                    If cell Is Nothing OrElse Not IndexOf(cell.RootId, index) Then
                        Continue For
                    End If

                    _primaryTypes(index) = If(cell.PrimaryType, "")
                    _annotated(index) = True
                Next
            End If

            If Not neurons Is Nothing Then
                For Each cell As Neurons In neurons
                    If cell Is Nothing OrElse Not IndexOf(cell.RootId, index) Then
                        Continue For
                    End If

                    _excitatory(index) = Not IsInhibitoryNeurotransmitter(cell.NtType)
                Next
            End If
        End Sub

        ''' <summary>
        ''' 递质类型是否为抑制性？(目前按照 GABA 为抑制性神经递质处理)
        ''' </summary>
        Public Shared Function IsInhibitoryNeurotransmitter(ntType As String) As Boolean
            If String.IsNullOrWhiteSpace(ntType) Then
                Return False
            End If

            Return String.Equals(ntType.Trim, "GABA", StringComparison.OrdinalIgnoreCase)
        End Function

#Region "annotation accessors"

        Public Function GetName(index As Integer) As String
            If _names Is Nothing Then Return ""
            Return If(_names(index), "")
        End Function

        Public Function GetGroup(index As Integer) As String
            If _groups Is Nothing Then Return ""
            Return If(_groups(index), "")
        End Function

        Public Function GetClass(index As Integer) As String
            If _classes Is Nothing Then Return ""
            Return If(_classes(index), "")
        End Function

        Public Function GetSuperClass(index As Integer) As String
            If _superClasses Is Nothing Then Return ""
            Return If(_superClasses(index), "")
        End Function

        Public Function GetPrimaryType(index As Integer) As String
            If _primaryTypes Is Nothing Then Return ""
            Return If(_primaryTypes(index), "")
        End Function

        ''' <summary>
        ''' 神经元在信息流上的位置（<c>classification.csv</c> 的 ``flow`` 列）。
        ''' </summary>
        ''' <remarks>
        ''' 这是这份数据集里唯一能直接区分"输入 / 输出"的注释：
        ''' ``afferent`` = 感觉输入神经元（把外界信号送进脑），
        ''' ``efferent`` = 运动 / 下行输出神经元（把脑的命令送出去），
        ''' ``intrinsic`` = 局部中间神经元。
        ''' 
        ''' 对于一个"让大脑接管外部设备"的用途来说，这三类正好对应
        ''' "刺激哪里" 与 "从哪里读出" —— 见 <see cref="GetFlowKind"/>。
        ''' </remarks>
        Public Enum NeuronFlow
            ''' <summary>没有 flow 注释</summary>
            Unknown
            ''' <summary>感觉输入（afferent）</summary>
            Afferent
            ''' <summary>运动输出（efferent）</summary>
            Efferent
            ''' <summary>局部中间神经元（intrinsic）</summary>
            Intrinsic
        End Enum

        ''' <summary>取神经元的 flow 注解文本（无注解时为空串）。</summary>
        Public Function GetFlow(index As Integer) As String
            If _flows Is Nothing OrElse index < 0 OrElse index >= _flows.Length Then Return ""
            Return If(_flows(index), "")
        End Function

        ''' <summary>把 flow 注解文本解析成枚举。</summary>
        Public Function GetFlowKind(index As Integer) As NeuronFlow
            Select Case GetFlow(index).Trim.ToLowerInvariant
                Case "afferent"
                    Return NeuronFlow.Afferent
                Case "efferent"
                    Return NeuronFlow.Efferent
                Case "intrinsic"
                    Return NeuronFlow.Intrinsic
                Case Else
                    Return NeuronFlow.Unknown
            End Select
        End Function

        ''' <summary>列出某一种 flow 的全部神经元索引（升序），可用于挑选感觉 / 运动神经元群。</summary>
        Public Function NeuronsOfFlow(flow As NeuronFlow) As Integer()
            Dim members As New List(Of Integer)(8192)

            For i As Integer = 0 To Size - 1
                If GetFlowKind(i) = flow Then
                    Call members.Add(i)
                End If
            Next

            Return members.ToArray()
        End Function

        ''' <summary>该神经元是否为兴奋性？(依据 neurons.csv 的 nt_type 判定，缺注释时默认兴奋性)</summary>
        Public Function IsExcitatory(index As Integer) As Boolean
            If _excitatory Is Nothing Then Return True
            Return _excitatory(index)
        End Function

        ''' <summary>该神经元是否关联到了注释信息？</summary>
        Public Function IsAnnotated(index As Integer) As Boolean
            If _annotated Is Nothing Then Return False
            Return _annotated(index)
        End Function

#End Region

        ''' <summary>
        ''' 统计出现次数最多的 group (用于在未指定刺激目标的时候自动挑选一个有意义的目标)。
        ''' </summary>
        Public Function SuggestStimulationGroup() As String
            If _groups Is Nothing OrElse Size = 0 Then
                Return ""
            End If

            Dim counter As New Dictionary(Of String, Integer)()

            For i As Integer = 0 To Size - 1
                Dim g As String = _groups(i)

                If String.IsNullOrEmpty(g) Then
                    Continue For
                End If

                Dim n As Integer

                counter(g) = If(counter.TryGetValue(g, n), n, 0) + 1
            Next

            If counter.Count = 0 Then
                Return ""
            End If

            Return counter.OrderByDescending(Function(kv) kv.Value).First.Key
        End Function

        Public Overrides Function ToString() As String
            Return $"neurons={Size}, annotated={AnnotatedCount}, missing={MissingAnnotationCount}, frozen={_frozen}"
        End Function

    End Class
End Namespace
