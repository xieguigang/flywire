Imports FlywireAI.Connectome
Imports FlywireAI.FAFBv783

Namespace Data

    ''' <summary>
    ''' 三维可视化所使用的果蝇大脑数据集：神经元索引与注释、三维坐标、主导脑区，
    ''' 以及连接表。
    ''' </summary>
    ''' <remarks>
    ''' 三个设计决策：
    ''' 
    ''' 1. <b>神经元索引复用 <see cref="ConnectomeIndex"/></b>（``root_id`` ↔ ``[0, N)`` 双向映射
    '''    以及 name / group / class / primary_type 注释）。这样三维视图里的"第 i 个点"与
    '''    SNN 仿真里的"第 i 个神经元"是同一个对象，两个子系统的结果可以直接互相印证；
    ''' 2. <b>属性用稀疏值类型数组</b>（``Double()`` / ``Integer()`` / ``String()``）而不是对象
    '''    列表：13 万个神经元如果每条属性都装箱，托管堆会多付出数倍的开销；
    ''' 3. <b>连接表用并行数组</b>（<see cref="Pre"/> / <see cref="Post"/> / ...）而不是对象列表：
    '''    534 万条连接用对象表示需要数 GB，用并行数组约 100 MB。
    ''' </remarks>
    Public Class BrainDataset

        ''' <summary>神经元索引与注释（``root_id`` ↔ 索引，name / group / class / primary_type）。</summary>
        Public Property Index As ConnectomeIndex

        ''' <summary>
        ''' 神经元坐标，按 ``[x0, y0, z0, x1, y1, z1, ...]`` 排列 (纳米)。
        ''' 没有坐标的神经元三个分量都是 ``NaN``。
        ''' </summary>
        Public Property Positions As Double()

        ''' <summary>每个神经元在 ``coordinates.csv`` 里被标记的坐标条数（0 表示没有坐标）。</summary>
        Public Property PositionMarks As Integer()

        ''' <summary>
        ''' 主导脑区索引，``-1`` 表示该神经元在脑区表里没有记录。
        ''' </summary>
        Public Property Neuropil As Integer()

        ''' <summary>脑区名称表（<see cref="Neuropil"/> 的元素指向这里）。</summary>
        Public Property NeuropilNames As String()

        ''' <summary>主导脑区的突触总数（用于排序 / 筛选）。</summary>
        Public Property NeuropilSynapses As Double()

        ''' <summary>每个神经元的递质类型（``neurons.csv`` 的 ``nt_type``，缺注释时为空串）。</summary>
        Public Property Neurotransmitters As String()

        ''' <summary>
        ''' 每个神经元的仿真活跃度（脉冲计数）；``Nothing`` 表示当前数据集没有活跃度数据。
        ''' </summary>
        Public Property Activity As Double()

        ''' <summary>活跃度数据的来源描述（用于界面状态栏）。</summary>
        Public Property ActivitySource As String = ""

        ''' <summary>连接的前突触神经元索引（并行数组）。</summary>
        Public Property Pre As Integer()

        ''' <summary>连接的后突触神经元索引（并行数组）。</summary>
        Public Property Post As Integer()

        ''' <summary>连接的突触数量（并行数组）。</summary>
        Public Property SynCount As Integer()

        ''' <summary>连接所在脑区的索引（指向 <see cref="ConnectionNeuropils"/>，``-1`` 表示未知）。</summary>
        Public Property ConnectionNeuropil As Integer()

        ''' <summary>连接的递质类型索引（指向 <see cref="ConnectionNeurotransmitters"/>）。</summary>
        Public Property ConnectionNtType As Integer()

        ''' <summary>连接的脑区名称表。</summary>
        Public Property ConnectionNeuropils As String()

        ''' <summary>连接的递质名称表。</summary>
        Public Property ConnectionNeurotransmitters As String()

        ''' <summary>数据集来源目录或描述（用于界面状态栏）。</summary>
        Public Property Source As String = ""

        ''' <summary>神经元总数 N。</summary>
        Public ReadOnly Property Units As Integer
            Get
                If Index Is Nothing Then Return 0
                Return Index.Size
            End Get
        End Property

        ''' <summary>连接总数。</summary>
        Public ReadOnly Property ConnectionCount As Integer
            Get
                If Pre Is Nothing Then Return 0
                Return Pre.Length
            End Get
        End Property

        ''' <summary>拥有坐标的神经元数量。</summary>
        Public ReadOnly Property PositionedCount As Integer
            Get
                If Positions Is Nothing Then Return 0

                Dim n As Integer = 0

                For i As Integer = 0 To (Positions.Length \ 3) - 1
                    If Not Double.IsNaN(Positions(i * 3)) Then
                        n += 1
                    End If
                Next

                Return n
            End Get
        End Property

        ''' <summary>是否带有仿真活跃度数据？</summary>
        Public ReadOnly Property HasActivity As Boolean
            Get
                Return Activity IsNot Nothing AndAlso Activity.Length = Units
            End Get
        End Property

        ''' <summary>该神经元是否有可用的三维坐标？</summary>
        Public Function HasPosition(index As Integer) As Boolean
            If Positions Is Nothing Then Return False

            Dim offset As Integer = index * 3

            If offset < 0 OrElse offset + 2 >= Positions.Length Then Return False

            Return Not Double.IsNaN(Positions(offset))
        End Function

        ''' <summary>取得神经元的坐标；没有坐标时返回无效值。</summary>
        Public Function GetPosition(index As Integer) As NeuronPosition
            If Not HasPosition(index) Then
                Return New NeuronPosition(Double.NaN, Double.NaN, Double.NaN)
            End If

            Dim offset As Integer = index * 3

            Return New NeuronPosition(Positions(offset), Positions(offset + 1), Positions(offset + 2))
        End Function

        ''' <summary>神经元的主导脑区名；未知时返回 ``(unknown)``。</summary>
        Public Function GetNeuropilName(index As Integer) As String
            If Neuropil Is Nothing OrElse NeuropilNames Is Nothing Then Return "(unknown)"

            Dim id As Integer = Neuropil(index)

            If id < 0 OrElse id >= NeuropilNames.Length Then Return "(unknown)"

            Return NeuropilNames(id)
        End Function

        ''' <summary>连接的脑区名；未知时返回 ``(unknown)``。</summary>
        Public Function GetConnectionNeuropilName(index As Integer) As String
            If ConnectionNeuropil Is Nothing OrElse ConnectionNeuropils Is Nothing Then Return "(unknown)"

            Dim id As Integer = ConnectionNeuropil(index)

            If id < 0 OrElse id >= ConnectionNeuropils.Length Then Return "(unknown)"

            Return ConnectionNeuropils(id)
        End Function

        ''' <summary>连接的递质名；未知时返回空串。</summary>
        Public Function GetConnectionNtTypeName(index As Integer) As String
            If ConnectionNtType Is Nothing OrElse ConnectionNeurotransmitters Is Nothing Then Return ""

            Dim id As Integer = ConnectionNtType(index)

            If id < 0 OrElse id >= ConnectionNeurotransmitters.Length Then Return ""

            Return ConnectionNeurotransmitters(id)
        End Function

        ''' <summary>该连接的终点是否都是有坐标的神经元（画线的前提）。</summary>
        Public Function HasBothEndsPositioned(index As Integer) As Boolean
            If Pre Is Nothing OrElse index < 0 OrElse index >= Pre.Length Then Return False

            Return HasPosition(Pre(index)) AndAlso HasPosition(Post(index))
        End Function

        Public Overrides Function ToString() As String
            If Index Is Nothing Then
                Return "empty dataset"
            End If

            Return $"{Units} neurons ({PositionedCount} positioned), {ConnectionCount} connections, " &
                $"neuropils={If(NeuropilNames Is Nothing, 0, NeuropilNames.Length)}"
        End Function

    End Class

End Namespace
