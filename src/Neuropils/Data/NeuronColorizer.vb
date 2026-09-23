Imports System.Drawing
Imports System.Text
Imports FlywireAI.Connectome
Imports Microsoft.VisualBasic.Imaging.Drawing2D.Colors

Namespace Data

    ''' <summary>
    ''' 点云的着色维度。
    ''' </summary>
    Public Enum NeuronColorDimension

        ''' <summary>主导脑区 (``neuropil_synapse_table.csv`` 的 argmax)。</summary>
        Neuropil = 0

        ''' <summary>神经递质类型 (``neurons.csv`` 的 ``nt_type``)。</summary>
        Neurotransmitter = 1

        ''' <summary>细胞类型 (``consolidated_cell_types.csv`` 的 ``primary_type``)。</summary>
        CellType = 2

        ''' <summary>分类层级 (``classification.csv`` 的 ``super_class``)。</summary>
        SuperClass = 3

        ''' <summary>仿真活跃度 (连续值 → 热力图，不是离散配色)。</summary>
        Activity = 4

    End Enum

    ''' <summary>图例中的一项。</summary>
    Public Class ColorLegendItem

        Public Property Key As String = ""
        Public Property Color As Color
        Public Property Count As Integer

        ''' <summary>该类别当前是否显示 (由侧栏的勾选框控制)。</summary>
        Public Property Visible As Boolean = True

        Public Overrides Function ToString() As String
            Return $"{Key} ({Count})"
        End Function

    End Class

    ''' <summary>
    ''' 把一个着色维度解析成"每个神经元一个颜色"以及配套的图例。
    ''' </summary>
    ''' <remarks>
    ''' 几个取舍：
    ''' 
    ''' * <b>颜色按类别名固定分配</b>：类别顺序以"神经元数量降序"决定 (大类的颜色最醒目)，
    '''   但分配表在构造时一次算好。这样切换显示/隐藏某个脑区时，其余脑区的颜色不会跳变；
    ''' * <b>类别数超过 <see cref="MaxLegendItems"/> 时合并尾部为 "(others)"</b>：
    '''   脑区约 80 个、细胞类型可达数千个，逐类给色既不可读也不可辨；
    ''' * <b>活跃度走热力图而不是离散配色</b>：连续量的分级配色会丢失动态范围，
    '''   这里直接复用渲染管线的 ``ColorScheme`` (256 级调色板纹理)。
    ''' </remarks>
    Public Class NeuronColorizer

        ''' <summary>图例与配色的最大类别数 (超出部分归入 "(others)")。</summary>
        Public Const DefaultMaxLegendItems As Integer = 24

        ''' <summary>合并尾部类别所使用的名字。</summary>
        Public Const OthersKey As String = "(others)"

        Private ReadOnly m_dataset As BrainDataset
        Private ReadOnly m_categories As Integer()
        Private ReadOnly m_categoryNames As String()
        Private ReadOnly m_categoryColors As Color()
        Private ReadOnly m_legend As New List(Of ColorLegendItem)()
        Private ReadOnly m_hidden As New HashSet(Of Integer)()

        ''' <summary>
        ''' 每个神经元的 html 颜色字符串。
        ''' </summary>
        ''' <remarks>
        ''' 字符串按类别驻留并复用引用：13 万个点如果各自持有独立的字符串实例，
        ''' 会白白产生 13 万个小字符串 (每个约 24 字节外加一次分配)。
        ''' </remarks>
        Private ReadOnly m_colorText As String()

        Public ReadOnly Property Dimension As NeuronColorDimension

        ''' <summary>该维度是否使用连续热力图着色。</summary>
        Public ReadOnly Property IsHeatMap As Boolean

        ''' <summary>神经元的活跃度 (热力图维度使用，其它维度为 ``Nothing``)。</summary>
        Public ReadOnly Property Intensity As Double()

        Private Sub New(dataset As BrainDataset, dimension As NeuronColorDimension)
            m_dataset = dataset
            Me.Dimension = dimension
            Me.IsHeatMap = (dimension = NeuronColorDimension.Activity)

            ' 类别表：每个神经元的类别索引 + 每类的名称与数量
            Dim categories = buildCategories(dataset, dimension)
            Dim categoryOf As Integer() = categories.CategoryOf
            Dim counts As Integer() = categories.Counts
            Dim names As String() = categories.Names

            m_categories = categoryOf
            m_categoryNames = names

            Dim palette As Color() = buildPalette(names.Length)

            m_categoryColors = palette

            Dim colorText As String() = New String(names.Length - 1) {}

            For i As Integer = 0 To names.Length - 1
                colorText(i) = $"#{palette(i).R:x2}{palette(i).G:x2}{palette(i).B:x2}"

                Call m_legend.Add(New ColorLegendItem With {
                    .Key = names(i),
                    .Color = palette(i),
                    .Count = counts(i)
                })
            Next

            If IsHeatMap Then
                Me.Intensity = dataset.Activity
            Else
                m_colorText = New String(dataset.Units - 1) {}

                For i As Integer = 0 To dataset.Units - 1
                    m_colorText(i) = colorText(categoryOf(i))
                Next
            End If
        End Sub

        ''' <summary>按维度构建一个着色器 (维度不可用时返回 ``Nothing``)。</summary>
        Public Shared Function Create(dataset As BrainDataset, dimension As NeuronColorDimension) As NeuronColorizer
            If dataset Is Nothing OrElse dataset.Index Is Nothing Then Return Nothing

            Select Case dimension
                Case NeuronColorDimension.Activity
                    ' 没有活跃度数据时退回主导脑区，避免界面上出现"全黑"的点云
                    If Not dataset.HasActivity Then
                        dimension = NeuronColorDimension.Neuropil
                    End If
            End Select

            Return New NeuronColorizer(dataset, dimension)
        End Function

        ''' <summary>图例项 (按神经元数量降序)。</summary>
        Public ReadOnly Property Legend As ColorLegendItem()
            Get
                Return m_legend.OrderByDescending(Function(item) item.Count).ToArray()
            End Get
        End Property

        ''' <summary>当前可见的类别数。</summary>
        Public ReadOnly Property VisibleCategoryCount As Integer
            Get
                Return m_categoryNames.Length - m_hidden.Count
            End Get
        End Property

        ''' <summary>某个图例项是否可见。</summary>
        Public Function IsVisible(item As ColorLegendItem) As Boolean
            Return Not m_hidden.Contains(categoryIndexOf(item.Key))
        End Function

        ''' <summary>按图例项切换该类别的显示。</summary>
        Public Sub SetVisible(item As ColorLegendItem, visible As Boolean)
            Dim index As Integer = categoryIndexOf(item.Key)

            If index < 0 Then Return

            If visible Then
                Call m_hidden.Remove(index)
            Else
                Call m_hidden.Add(index)
            End If

            item.Visible = visible
        End Sub

        ''' <summary>只显示给定类别，隐藏其余类别。</summary>
        Public Sub SetOnly(items As IEnumerable(Of ColorLegendItem))
            Dim keep As New HashSet(Of Integer)()

            If items IsNot Nothing Then
                For Each item As ColorLegendItem In items
                    Call keep.Add(categoryIndexOf(item.Key))
                Next
            End If

            Call m_hidden.Clear()

            For i As Integer = 0 To m_categoryNames.Length - 1
                If Not keep.Contains(i) Then
                    Call m_hidden.Add(i)
                End If
            Next

            Call syncLegendVisibility()
        End Sub

        ''' <summary>显示全部类别。</summary>
        Public Sub ShowAll()
            Call m_hidden.Clear()
            Call syncLegendVisibility()
        End Sub

        ''' <summary>该神经元在当前筛选下是否可见。</summary>
        Public Function IsNeuronVisible(index As Integer) As Boolean
            If IsHeatMap Then Return True
            If m_hidden.Count = 0 Then Return True

            Return Not m_hidden.Contains(m_categories(index))
        End Function

        ''' <summary>神经元颜色的 html 形式 (离散维度)。</summary>
        Public Function GetColorText(index As Integer) As String
            If IsHeatMap OrElse m_colorText Is Nothing Then Return Nothing

            Return m_colorText(index)
        End Function

        ''' <summary>
        ''' 神经元的离散颜色 (不含透明度)。
        ''' </summary>
        ''' <remarks>
        ''' 连线的着色需要把"前突触神经元的颜色"再叠上自己的透明度，
        ''' 因此这里返回颜色值而不是 html 字符串。
        ''' </remarks>
        Public Function GetColor(index As Integer) As Color
            If IsHeatMap OrElse m_categoryColors Is Nothing OrElse m_categories Is Nothing Then
                Return Color.Empty
            End If

            Return m_categoryColors(m_categories(index))
        End Function

        ''' <summary>类别颜色表 (索引与 <see cref="Legend"/> 的类别索引一致)。</summary>
        Public Function GetCategoryColor(categoryIndex As Integer) As Color
            If m_categoryColors Is Nothing OrElse categoryIndex < 0 OrElse categoryIndex >= m_categoryColors.Length Then
                Return Color.Empty
            End If

            Return m_categoryColors(categoryIndex)
        End Function

        ''' <summary>神经元所属类别的名字。</summary>
        Public Function GetCategoryName(index As Integer) As String
            If m_categoryNames Is Nothing OrElse m_categoryNames.Length = 0 Then Return ""

            Return m_categoryNames(m_categories(index))
        End Function

        ''' <summary>某个类别当前是否被隐藏 (用于快速过滤)。</summary>
        Private Function categoryIndexOf(key As String) As Integer
            For i As Integer = 0 To m_categoryNames.Length - 1
                If String.Equals(m_categoryNames(i), key, StringComparison.Ordinal) Then
                    Return i
                End If
            Next

            Return -1
        End Function

        Private Sub syncLegendVisibility()
            For Each item As ColorLegendItem In m_legend
                item.Visible = Not m_hidden.Contains(categoryIndexOf(item.Key))
            Next
        End Sub

        ''' <summary>
        ''' 把维度解析成类别表：每个神经元的类别索引、类别名与类别计数。
        ''' </summary>
        ''' <remarks>
        ''' 只有离散维度会走出"类别"这一层；活跃度维度直接把连续值交给热力图，
        ''' 这里返回一个单类别表作为占位。
        ''' </remarks>
        Private Shared Function buildCategories(dataset As BrainDataset,
                                               dimension As NeuronColorDimension) As (CategoryOf As Integer(), Counts As Integer(), Names As String())

            Dim raw As String() = New String(dataset.Units - 1) {}
            Dim index As ConnectomeIndex = dataset.Index

            Select Case dimension
                Case NeuronColorDimension.Neuropil
                    For i As Integer = 0 To dataset.Units - 1
                        raw(i) = dataset.GetNeuropilName(i)
                    Next
                Case NeuronColorDimension.Neurotransmitter
                    For i As Integer = 0 To dataset.Units - 1
                        raw(i) = dataset.Neurotransmitters(i)
                    Next
                Case NeuronColorDimension.CellType
                    For i As Integer = 0 To dataset.Units - 1
                        raw(i) = index.GetPrimaryType(i)
                    Next
                Case NeuronColorDimension.SuperClass
                    For i As Integer = 0 To dataset.Units - 1
                        raw(i) = index.GetSuperClass(i)
                    Next
                Case Else
                    For i As Integer = 0 To dataset.Units - 1
                        raw(i) = "(activity)"
                    Next
            End Select

            ' 1) 统计类别计数 (空值归入 "(unannotated)")
            Dim counter As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
            Dim assign As Integer() = New Integer(raw.Length - 1) {}

            For i As Integer = 0 To raw.Length - 1
                Dim key As String = If(String.IsNullOrWhiteSpace(raw(i)), "(unannotated)", raw(i).Trim)
                Dim n As Integer

                If Not counter.TryGetValue(key, n) Then
                    n = 0
                    Call counter.Add(key, 0)
                End If

                counter(key) = n + 1
                raw(i) = key
            Next

            ' 2) 类别顺序：数量降序 (大类的颜色最醒目)，尾部合并为 "(others)"
            Dim ordered As String() = counter _
                .OrderByDescending(Function(kv) kv.Value) _
                .ThenBy(Function(kv) kv.Key, StringComparer.Ordinal) _
                .Select(Function(kv) kv.Key) _
                .ToArray()

            Dim limit As Integer = System.Math.Max(1, DefaultMaxLegendItems)
            Dim visible As String()
            Dim hasOthers As Boolean = ordered.Length > limit

            If hasOthers Then
                visible = ordered.Take(limit - 1).ToArray()
            Else
                visible = ordered
            End If

            Dim nameList As New List(Of String)(visible)

            If hasOthers Then
                Call nameList.Add(OthersKey)
            End If

            Dim ids As New Dictionary(Of String, Integer)(StringComparer.Ordinal)

            For i As Integer = 0 To nameList.Count - 1
                Call ids.Add(nameList(i), i)
            Next

            Dim total As Integer() = New Integer(nameList.Count - 1) {}

            For i As Integer = 0 To raw.Length - 1
                Dim category As Integer

                If Not ids.TryGetValue(raw(i), category) Then
                    category = nameList.Count - 1     ' (others)
                End If

                assign(i) = category
                total(category) += 1
            Next

            Return (assign, total, nameList.ToArray())
        End Function

        ''' <summary>
        ''' 生成类别配色：先用 ColorBrewer 的定性色板 (区分度高)，
        ''' 类别数超过色板长度时用黄金角旋转色相继续补足。
        ''' </summary>
        Private Shared Function buildPalette(count As Integer) As Color()
            Dim palette As Color() = ColorBrewer.Set3
            Dim result(count - 1) As Color

            For i As Integer = 0 To count - 1
                ' 最后一个类别是"(others)"：固定用中性灰，避免与真实类别抢注意力
                If i = count - 1 AndAlso count > 1 Then
                    result(i) = Color.FromArgb(160, 160, 160)
                    Continue For
                End If

                If i < palette.Length Then
                    result(i) = palette(i)
                Else
                    ' 黄金角 (137.508°) 旋转色相：连续生成的相邻颜色差异最大
                    Dim hue As Double = (i * 137.508) Mod 360.0

                    result(i) = hsv(hue, 0.62, 0.85)
                End If
            Next

            Return result
        End Function

        ''' <summary>HSV → RGB (h ∈ [0, 360), s/v ∈ [0, 1])。</summary>
        Private Shared Function hsv(h As Double, s As Double, v As Double) As Color
            Dim c As Double = v * s
            Dim hp As Double = h / 60.0
            Dim x As Double = c * (1 - System.Math.Abs((hp Mod 2.0) - 1))

            Dim r As Double = 0
            Dim g As Double = 0
            Dim b As Double = 0

            Select Case CInt(System.Math.Floor(hp)) Mod 6
                Case 0 : r = c : g = x
                Case 1 : r = x : g = c
                Case 2 : g = c : b = x
                Case 3 : g = x : b = c
                Case 4 : r = x : b = c
                Case Else : r = c : b = x
            End Select

            Dim m As Double = v - c

            Return Color.FromArgb(
                CInt(System.Math.Round((r + m) * 255)),
                CInt(System.Math.Round((g + m) * 255)),
                CInt(System.Math.Round((b + m) * 255)))
        End Function

        Public Overrides Function ToString() As String
            If IsHeatMap Then
                Return $"activity heat map (source: {If(String.IsNullOrEmpty(m_dataset.ActivitySource), "in-memory simulation", m_dataset.ActivitySource)})"
            End If

            Dim sb As New StringBuilder()

            Call sb.Append($"{Dimension}: {m_categoryNames.Length} categories")

            If m_hidden.Count > 0 Then
                Call sb.Append($", {m_hidden.Count} hidden")
            End If

            Return sb.ToString
        End Function

    End Class

End Namespace
