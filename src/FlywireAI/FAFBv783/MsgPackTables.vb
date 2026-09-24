Imports System.IO
Imports Microsoft.VisualBasic.Data.IO.MessagePack.Serialization

Namespace FAFBv783

    ''' <summary>
    ''' FAFB v783 数据源表的 <b>msgpack 列式转储</b>：一张 CSV 一张表，一列一个定型数组。
    ''' </summary>
    ''' <remarks>
    ''' <b>为什么是"列式"而不是"逐行对象"</b>：msgpack 的序列化器对<b>基础类型数组</b>走的是
    ''' 最快的那条路径（直接写 msgpack 的 ARRAY/FLOAT/INT 格式头），而对<b>对象</b>走的是
    ''' 按属性反射的 MAP / ARRAY 路径。5,342,446 行连接表在逐行对象路径下光是属性读写就要
    ''' 几千万次反射调用，而列式只是 2,600 万个定长值的连续写入/读出 ——
    ''' 这才是"省掉文本解析"该有的量级。
    ''' 
    ''' <b>定型</b>的意思是：转储时已经把 ASCII 文本解析成 <c>Long / Double / Integer / String</c>，
    ''' 读回来之后不再需要 <c>Double.Parse</c>、<c>Long.TryParse</c> 与 <c>Split(",")</c>。
    ''' 
    ''' 每张表都保留 CSV 的<b>全部列</b>（不做任何丢弃/聚合），因此转储包可以随时被别的功能复用；
    ''' 具体的语义（坐标取平均、脑区取最大值等）仍然由使用方（Neuropils）决定。
    ''' 
    ''' <b>每个属性都要挂 <c>&lt;MessagePackMember&gt;</c></b>：msgpack 的默认布局是
    ''' <see cref="SerializationMethod.Array"/>，只有带这个标记的属性才会进序列化集合，
    ''' 漏标的话写出来的会是一个空数组（实测每个 entry 只有 1 字节，读回来全是 Nothing）。
    ''' 编号只用于标识，实际顺序由属性声明顺序决定，因此<b>不要</b>随意调换属性次序。
    ''' </remarks>

    ''' <summary>``names.csv``：神经元主索引 (root_id / 名称 / 分组)。</summary>
    Public Class CellNamesPack

        <MessagePackMember(1)>
        Public Property RootId As Long()

        <MessagePackMember(2)>
        Public Property Name As String()

        <MessagePackMember(3)>
        Public Property Group As String()

        Public Shared Function FromRecords(rows As List(Of CellNames)) As CellNamesPack
            Dim count As Integer = If(rows Is Nothing, 0, rows.Count)
            Dim pack As New CellNamesPack With {
                .RootId = New Long(count - 1) {},
                .Name = New String(count - 1) {},
                .Group = New String(count - 1) {}
            }

            For i As Integer = 0 To count - 1
                pack.RootId(i) = rows(i).RootId
                pack.Name(i) = rows(i).Name
                pack.Group(i) = rows(i).Group
            Next

            Return pack
        End Function

        Public Function ToRecords() As List(Of CellNames)
            Dim count As Integer = If(RootId Is Nothing, 0, RootId.Length)
            Dim rows As New List(Of CellNames)(count)

            For i As Integer = 0 To count - 1
                Call rows.Add(New CellNames With {
                    .RootId = RootId(i),
                    .Name = if(Name Is Nothing, Nothing, Name(i)),
                    .Group = if(Group Is Nothing, Nothing, Group(i))
                })
            Next

            Return rows
        End Function

    End Class

    ''' <summary>``classification.csv``：解剖学分类。</summary>
    Public Class ClassificationPack

        <MessagePackMember(1)>
        Public Property RootId As Long()

        <MessagePackMember(2)>
        Public Property Flow As String()

        <MessagePackMember(3)>
        Public Property SuperClass As String()

        <MessagePackMember(4)>
        Public Property [Class] As String()

        <MessagePackMember(5)>
        Public Property SubClass As String()

        <MessagePackMember(6)>
        Public Property Hemilineage As String()

        <MessagePackMember(7)>
        Public Property Side As String()

        <MessagePackMember(8)>
        Public Property Nerve As String()

        Public Shared Function FromRecords(rows As List(Of Classification)) As ClassificationPack
            Dim count As Integer = If(rows Is Nothing, 0, rows.Count)
            Dim pack As New ClassificationPack With {
                .RootId = New Long(count - 1) {},
                .Flow = New String(count - 1) {},
                .SuperClass = New String(count - 1) {},
                .[Class] = New String(count - 1) {},
                .SubClass = New String(count - 1) {},
                .Hemilineage = New String(count - 1) {},
                .Side = New String(count - 1) {},
                .Nerve = New String(count - 1) {}
            }

            For i As Integer = 0 To count - 1
                pack.RootId(i) = rows(i).RootId
                pack.Flow(i) = rows(i).Flow
                pack.SuperClass(i) = rows(i).SuperClass
                pack.[Class](i) = rows(i).[Class]
                pack.SubClass(i) = rows(i).SubClass
                pack.Hemilineage(i) = rows(i).Hemilineage
                pack.Side(i) = rows(i).Side
                pack.Nerve(i) = rows(i).Nerve
            Next

            Return pack
        End Function

        Public Function ToRecords() As List(Of Classification)
            Dim count As Integer = If(RootId Is Nothing, 0, RootId.Length)
            Dim rows As New List(Of Classification)(count)

            For i As Integer = 0 To count - 1
                Call rows.Add(New Classification With {
                    .RootId = RootId(i),
                    .Flow = item(Flow, i),
                    .SuperClass = item(SuperClass, i),
                    .Class = item([Class], i),
                    .SubClass = item(SubClass, i),
                    .Hemilineage = item(Hemilineage, i),
                    .Side = item(Side, i),
                    .Nerve = item(Nerve, i)
                })
            Next

            Return rows
        End Function

    End Class

    ''' <summary>``consolidated_cell_types.csv``：细胞类型。</summary>
    Public Class CellTypesPack

        <MessagePackMember(1)>
        Public Property RootId As Long()

        <MessagePackMember(2)>
        Public Property PrimaryType As String()

        <MessagePackMember(3)>
        Public Property AdditionalTypes As String()

        Public Shared Function FromRecords(rows As List(Of CellTypes)) As CellTypesPack
            Dim count As Integer = If(rows Is Nothing, 0, rows.Count)
            Dim pack As New CellTypesPack With {
                .RootId = New Long(count - 1) {},
                .PrimaryType = New String(count - 1) {},
                .AdditionalTypes = New String(count - 1) {}
            }

            For i As Integer = 0 To count - 1
                pack.RootId(i) = rows(i).RootId
                pack.PrimaryType(i) = rows(i).PrimaryType
                pack.AdditionalTypes(i) = rows(i).AdditionalTypes
            Next

            Return pack
        End Function

        Public Function ToRecords() As List(Of CellTypes)
            Dim count As Integer = If(RootId Is Nothing, 0, RootId.Length)
            Dim rows As New List(Of CellTypes)(count)

            For i As Integer = 0 To count - 1
                Call rows.Add(New CellTypes With {
                    .RootId = RootId(i),
                    .PrimaryType = item(PrimaryType, i),
                    .AdditionalTypes = item(AdditionalTypes, i)
                })
            Next

            Return rows
        End Function

    End Class

    ''' <summary>``neurons.csv``：递质类型与打分。</summary>
    Public Class NeuronsPack

        <MessagePackMember(1)>
        Public Property RootId As Long()

        <MessagePackMember(2)>
        Public Property Group As String()

        <MessagePackMember(3)>
        Public Property NtType As String()

        <MessagePackMember(4)>
        Public Property NtTypeScore As Double()

        <MessagePackMember(5)>
        Public Property DaAvg As Double()

        <MessagePackMember(6)>
        Public Property SerAvg As Double()

        <MessagePackMember(7)>
        Public Property GabaAvg As Double()

        <MessagePackMember(8)>
        Public Property GlutAvg As Double()

        <MessagePackMember(9)>
        Public Property AchAvg As Double()

        <MessagePackMember(10)>
        Public Property OctAvg As Double()

        Public Shared Function FromRecords(rows As List(Of Neurons)) As NeuronsPack
            Dim count As Integer = If(rows Is Nothing, 0, rows.Count)
            Dim pack As New NeuronsPack With {
                .RootId = New Long(count - 1) {},
                .Group = New String(count - 1) {},
                .NtType = New String(count - 1) {},
                .NtTypeScore = New Double(count - 1) {},
                .DaAvg = New Double(count - 1) {},
                .SerAvg = New Double(count - 1) {},
                .GabaAvg = New Double(count - 1) {},
                .GlutAvg = New Double(count - 1) {},
                .AchAvg = New Double(count - 1) {},
                .OctAvg = New Double(count - 1) {}
            }

            For i As Integer = 0 To count - 1
                pack.RootId(i) = rows(i).RootId
                pack.Group(i) = rows(i).Group
                pack.NtType(i) = rows(i).NtType
                pack.NtTypeScore(i) = rows(i).NtTypeScore
                pack.DaAvg(i) = rows(i).DaAvg
                pack.SerAvg(i) = rows(i).SerAvg
                pack.GabaAvg(i) = rows(i).GabaAvg
                pack.GlutAvg(i) = rows(i).GlutAvg
                pack.AchAvg(i) = rows(i).AchAvg
                pack.OctAvg(i) = rows(i).OctAvg
            Next

            Return pack
        End Function

        Public Function ToRecords() As List(Of Neurons)
            Dim count As Integer = If(RootId Is Nothing, 0, RootId.Length)
            Dim rows As New List(Of Neurons)(count)

            For i As Integer = 0 To count - 1
                Call rows.Add(New Neurons With {
                    .RootId = RootId(i),
                    .Group = item(Group, i),
                    .NtType = item(NtType, i),
                    .NtTypeScore = number(NtTypeScore, i),
                    .DaAvg = number(DaAvg, i),
                    .SerAvg = number(SerAvg, i),
                    .GabaAvg = number(GabaAvg, i),
                    .GlutAvg = number(GlutAvg, i),
                    .AchAvg = number(AchAvg, i),
                    .OctAvg = number(OctAvg, i)
                })
            Next

            Return rows
        End Function

    End Class

    ''' <summary>
    ''' ``coordinates.csv``：标记位置。
    ''' </summary>
    ''' <remarks>
    ''' <c>Position</c> 保留原始字符串 (``[x y z]``) 而不是预先拆成三个 Double：
    ''' 坐标解析与"同一神经元多条标记取平均"的语义属于使用方，转储层只负责把文本搬过来。
    ''' 这张表只有 13.9 MB，本来也不是加载瓶颈。
    ''' </remarks>
    Public Class CoordinatesPack

        <MessagePackMember(1)>
        Public Property RootId As Long()

        <MessagePackMember(2)>
        Public Property Position As String()

        <MessagePackMember(3)>
        Public Property SupervoxelId As Long()

        Public Shared Function FromRecords(rows As List(Of Coordinates)) As CoordinatesPack
            Dim count As Integer = If(rows Is Nothing, 0, rows.Count)
            Dim pack As New CoordinatesPack With {
                .RootId = New Long(count - 1) {},
                .Position = New String(count - 1) {},
                .SupervoxelId = New Long(count - 1) {}
            }

            For i As Integer = 0 To count - 1
                pack.RootId(i) = rows(i).RootId
                pack.Position(i) = rows(i).Position
                pack.SupervoxelId(i) = rows(i).SupervoxelId
            Next

            Return pack
        End Function

        Public Function ToRecords() As List(Of Coordinates)
            Dim count As Integer = If(RootId Is Nothing, 0, RootId.Length)
            Dim rows As New List(Of Coordinates)(count)

            For i As Integer = 0 To count - 1
                Call rows.Add(New Coordinates With {
                    .RootId = RootId(i),
                    .Position = item(Position, i),
                    .SupervoxelId = if(SupervoxelId Is Nothing, 0L, SupervoxelId(i))
                })
            Next

            Return rows
        End Function

    End Class

    ''' <summary>
    ''' ``neuropil_synapse_table.csv``：321 列的脑区突触表。
    ''' </summary>
    ''' <remarks>
    ''' <b>321 列怎么存</b>：<c>Columns</c> 是 CSV 的表头（列全名，例如
    ''' ``input synapses in AL_L``），而<b>每一列单独占一个 msgpack 项</b>
    ''' (见 <see cref="FafbMsgPackStorage.NeuropilColumnKey"/>)。
    ''' 
    ''' 为什么不塞进一个二维数组：用方（界面）只需要其中的 158 个"脑区突触数"列，
    ''' 一次性读 321 × 134,181 个数等于把两倍于需要的数值都读一遍（实测那会让这一步
    ''' 从 0.85 s 变成 7.9 s，比直接读 csv 还慢一个数量级）。
    ''' 列全名必须留着：脑区名只出现在列名里，使用方要按
    ''' <c>input synapses in &lt;region&gt;</c> 前缀把列挑出来，再按列号去取对应的项。
    ''' </remarks>
    Public Class NeuropilTablePack

        ''' <summary>每行神经元的 root_id。</summary>
        <MessagePackMember(1)>
        Public Property RootId As Long()

        ''' <summary>CSV 表头 (321 个列全名，顺序与文件一致)。</summary>
        <MessagePackMember(2)>
        Public Property Columns As String()

        ''' <summary>列数 (= <see cref="Columns"/> 的长度，也就是包里有多少个列项)。</summary>
        <MessagePackMember(3)>
        Public Property ColumnCount As Integer

        ''' <summary>行数 (= 神经元行数)。</summary>
        Public Function RowCount() As Integer
            Return If(RootId Is Nothing, 0, RootId.Length)
        End Function

    End Class

    ''' <summary>
    ''' 脑区表的<b>单列</b>数值 (``Values(行号)``)。
    ''' </summary>
    ''' <remarks>
    ''' 单独成类是为了让"一列"能独立成为一个 msgpack 项：
    ''' 用方只读它需要的那几列，不必整张 321 列的表都过一遍。
    ''' </remarks>
    Public Class DoubleColumnPack

        <MessagePackMember(1)>
        Public Property Values As Double()

    End Class

    ''' <summary>一整列 Long（连接表的 root_id 列）。</summary>
    Public Class LongColumnPack

        <MessagePackMember(1)>
        Public Property Values As Long()

    End Class

    ''' <summary>一整列 Integer（连接表字典化之后的编号列）。</summary>
    Public Class IntegerColumnPack

        <MessagePackMember(1)>
        Public Property Values As Integer()

    End Class

    ''' <summary>
    ''' ``connections_princeton.csv``：534 万行突触连接。
    ''' </summary>
    ''' <remarks>
    ''' 这一项里只有<b>元数据</b>（行数 + 两个名称表），5 个数据列各自独立成项
    ''' (见 <see cref="FafbMsgPackStorage.ConnectionColumnKey"/>)：
    ''' 用方按需取列，而"一列"这种形状也正好落在 msgpack 库的基础类型数组快路径上。
    ''' 
    ''' 两个分类列 (``neuropil`` / ``nt_type``) 在转储时<b>已经字典化</b>：
    ''' 存的是整数编号 + 名称表，而不是 1,068 万个字符串对象。
    ''' </remarks>
    Public Class ConnectionsPack

        ''' <summary>连接条数。</summary>
        <MessagePackMember(1)>
        Public Property RowCount As Integer

        ''' <summary>``neuropil`` 的名称表（编号 → 名称）。</summary>
        <MessagePackMember(2)>
        Public Property NeuropilNames As String()

        ''' <summary>``nt_type`` 的名称表（编号 → 名称）。</summary>
        <MessagePackMember(3)>
        Public Property NtNames As String()

        ''' <summary>取某个脑区编号对应的名称 (越界时返回空串)。</summary>
        Public Function NeuropilOf(code As Integer) As String
            If NeuropilNames Is Nothing OrElse code < 0 OrElse code >= NeuropilNames.Length Then Return ""

            Return NeuropilNames(code)
        End Function

        ''' <summary>取某个递质编号对应的名称 (越界时返回空串)。</summary>
        Public Function NeurotransmitterOf(code As Integer) As String
            If NtNames Is Nothing OrElse code < 0 OrElse code >= NtNames.Length Then Return ""

            Return NtNames(code)
        End Function

    End Class

End Namespace

Namespace FAFBv783

    ''' <summary>列式表取值的小工具（数组缺列 / 越界时给出类型默认值，不让转储包把使用方带崩）。</summary>
    Friend Module PackColumns

        Friend Function item(values As String(), i As Integer) As String
            If values Is Nothing OrElse i < 0 OrElse i >= values.Length Then Return Nothing

            Return values(i)
        End Function

        Friend Function number(values As Double(), i As Integer) As Double
            If values Is Nothing OrElse i < 0 OrElse i >= values.Length Then Return 0

            Return values(i)
        End Function

    End Module

End Namespace
