Imports System.Linq
Imports System.Runtime.CompilerServices

Namespace FAFBv783

    ''' <summary>
    ''' 单个神经元的 SWC 骨架模型：节点表 + 双向的父子树结构 + 文件元数据 + 几何统计。
    ''' 
    ''' 数据来源为 ``sk_lod1_783_healed.zip`` 之中的 ``&lt;root_id&gt;.swc`` 文件，
    ''' 解析器请参考 <see cref="SwcParser"/>。
    ''' </summary>
    ''' <remarks>
    ''' 节点坐标与半径按照文件原值存放 (``Double``)，单位由 <see cref="Unit"/> 进行描述，
    ''' 同时提供 <see cref="CableLengthNm"/> / <see cref="CableLengthMicrons"/> 之类的换算访问器。
    ''' </remarks>
    Public Class SwcSkeleton

        ''' <summary>
        ''' 骨架在压缩包之中的条目名，例如 ``720575940590515268.swc``。
        ''' </summary>
        Public Property EntryName As String

        ''' <summary>
        ''' 骨架所对应的 FlyWire Root ID；``-1`` 表示无法从 Meta 或者条目名之中解析出来。
        ''' </summary>
        Public Property RootId As Long = -1

        ''' <summary>
        ''' 文件头部 ``# Meta: {...}`` 行所声明的元数据。
        ''' </summary>
        Public Property Meta As SwcMeta

        ''' <summary>
        ''' 文件头部 ``# Labels:`` 段所声明的节点类型编号定义。
        ''' </summary>
        Public ReadOnly Property Labels As Dictionary(Of Integer, String) = New Dictionary(Of Integer, String)

        ''' <summary>
        ''' 骨架的全部节点 (按照文件之中的出现顺序)。
        ''' </summary>
        Public ReadOnly Property Nodes As List(Of SwcNode) = New List(Of SwcNode)

        ''' <summary>
        ''' 文件头部的全部注释行 (``#`` 开头，保留原始文本以便追溯数据来源)。
        ''' </summary>
        Public ReadOnly Property Comments As List(Of String) = New List(Of String)

        ''' <summary>
        ''' 解析过程之中所收集到的警告信息 (重复的节点编号、缺失的父节点等)。
        ''' </summary>
        Public ReadOnly Property Warnings As List(Of String) = New List(Of String)

        ReadOnly _index As Dictionary(Of Integer, SwcNode) = New Dictionary(Of Integer, SwcNode)()

        ''' <summary>
        ''' 节点总数。
        ''' </summary>
        Public ReadOnly Property Count As Integer
            Get
                Return Nodes.Count
            End Get
        End Property

        ''' <summary>
        ''' 骨架数据的单位：优先采用 <see cref="Meta"/> 所声明的单位。
        ''' 
        ''' (当 Meta 缺失或者无法识别的时候返回 <see cref="SwcUnits.Unknown"/>，此时换算函数
        ''' 会按照最安全的假设，即按照纳米来处理)
        ''' </summary>
        Public ReadOnly Property Unit As SwcUnits
            Get
                If Meta Is Nothing Then
                    Return SwcUnits.Unknown
                Else
                    Return Meta.Unit
                End If
            End Get
        End Property

        ''' <summary>
        ''' 通过节点编号获取节点 (``O(1)``)。
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function GetNode(id As Integer) As SwcNode
            Dim node As SwcNode = Nothing

            Call _index.TryGetValue(id, node)

            Return node
        End Function

        ''' <summary>
        ''' 所有的根节点 (``parent = -1``)。
        ''' </summary>
        Public ReadOnly Property Roots As SwcNode()
            Get
                Return Nodes.Where(Function(n) n.Parent < 0).ToArray
            End Get
        End Property

        ''' <summary>
        ''' 骨架的根节点 (通常是 soma 节点)；没有根节点的时候返回 Nothing，存在多个根节点的时候返回文本编号最小的那一个。
        ''' </summary>
        Public ReadOnly Property Root As SwcNode
            Get
                Dim roots As SwcNode() = Me.Roots

                If roots.Length = 0 Then
                    Return Nothing
                Else
                    Return roots.OrderBy(Function(n) n.Id).First
                End If
            End Get
        End Property

        ''' <summary>
        ''' 按照类型编号 (label) 查询节点。
        ''' </summary>
        Public Function GetNodesByLabel(label As Integer) As SwcNode()
            Return Nodes.Where(Function(n) n.Label = label).ToArray
        End Function

        ''' <summary>
        ''' 类型编号的语义名称 (例如 ``1`` -> ``soma``)，未定义的时候返回空字符串。
        ''' </summary>
        Public Function GetLabelName(label As Integer) As String
            Dim name As String = Nothing

            If Labels.TryGetValue(label, name) Then
                Return name
            Else
                Return ""
            End If
        End Function

        ''' <summary>
        ''' soma 节点 (类型编号为 1)。
        ''' </summary>
        Public ReadOnly Property SomaNodes As SwcNode()
            Get
                Return GetNodesByLabel(1)
            End Get
        End Property

        ''' <summary>
        ''' 分支点 (类型编号为 5，或者具有多于一个子节点的节点)。
        ''' </summary>
        Public ReadOnly Property ForkPoints As SwcNode()
            Get
                Return Nodes.Where(Function(n) n.Label = 5 OrElse n.Children.Count > 1).ToArray
            End Get
        End Property

        ''' <summary>
        ''' 末端点 (类型编号为 6，或者没有任何子节点的非根节点)。
        ''' </summary>
        Public ReadOnly Property EndPoints As SwcNode()
            Get
                Return Nodes.Where(Function(n) n.Label = 6 OrElse (n.Children.Count = 0 AndAlso n.Parent >= 0)).ToArray
            End Get
        End Property

        ''' <summary>
        ''' 节点类型编号的直方图。
        ''' </summary>
        Public ReadOnly Property NodeTypeHistogram As Dictionary(Of Integer, Integer)
            Get
                Return Nodes _
                    .GroupBy(Function(n) n.Label) _
                    .ToDictionary(Function(g) g.Key,
                                  Function(g)
                                      Return g.Count
                                  End Function)
            End Get
        End Property

#Region "geometric statistics"

        ''' <summary>
        ''' 骨架的总 cable 长度 (按照骨架的单位)；定义为全部节点到其父节点的空间距离之和。
        ''' </summary>
        ''' <remarks>该属性每次访问都会重新计算，复杂度为 ``O(n)``。</remarks>
        Public ReadOnly Property CableLength As Double
            Get
                Dim L As Double = 0

                For Each node As SwcNode In Nodes
                    If Not node.ParentNode Is Nothing Then
                        L += node.DistanceToParent()
                    End If
                Next

                Return L
            End Get
        End Property

        ''' <summary>
        ''' 骨架的总 cable 长度 (纳米)。
        ''' </summary>
        Public ReadOnly Property CableLengthNm As Double
            Get
                Return CableLength.ToNanometer(Unit)
            End Get
        End Property

        ''' <summary>
        ''' 骨架的总 cable 长度 (微米)。
        ''' </summary>
        Public ReadOnly Property CableLengthMicrons As Double
            Get
                Return CableLength.ToMicrometer(Unit)
            End Get
        End Property

        ''' <summary>
        ''' 全部节点的最大半径 (按照骨架的单位)。
        ''' </summary>
        Public ReadOnly Property MaxRadius As Double
            Get
                If Nodes.Count = 0 Then
                    Return 0
                Else
                    Return Nodes.Max(Function(n) n.Radius)
                End If
            End Get
        End Property

        ''' <summary>
        ''' 骨架的坐标包围盒 (按照骨架的单位)；没有任何节点的时候返回 Nothing。
        ''' </summary>
        Public ReadOnly Property BoundingBox As SwcBoundingBox
            Get
                If Nodes.Count = 0 Then
                    Return Nothing
                End If

                Return New SwcBoundingBox With {
                    .MinX = Nodes.Min(Function(n) n.X),
                    .MaxX = Nodes.Max(Function(n) n.X),
                    .MinY = Nodes.Min(Function(n) n.Y),
                    .MaxY = Nodes.Max(Function(n) n.Y),
                    .MinZ = Nodes.Min(Function(n) n.Z),
                    .MaxZ = Nodes.Max(Function(n) n.Z)
                }
            End Get
        End Property

#End Region

        ''' <summary>
        ''' 将节点添加到骨架之中 (由 <see cref="SwcParser"/> 调用)，重复的节点编号会被忽略并记录警告。
        ''' </summary>
        Public Sub AddNode(node As SwcNode)
            If node Is Nothing Then
                Return
            End If

            If _index.ContainsKey(node.Id) Then
                Call Warnings.Add($"duplicated node id: {node.Id}")

                Return
            End If

            Call Nodes.Add(node)
            Call _index.Add(node.Id, node)
        End Sub

        ''' <summary>
        ''' 建立节点之间的父子关系 (由 <see cref="SwcParser"/> 在读取完所有的数据行之后调用)。
        ''' 
        ''' 骨架文件之中的父节点编号可能大于子节点编号 (前向引用)，所以这里是在全部节点都已经
        ''' 读取完毕之后，通过节点编号字典进行第二遍的绑定。
        ''' </summary>
        Public Sub BuildTree()
            Dim unit As SwcUnits = Me.Unit

            ' 头部的 Meta 段可能出现在数据行之前，所以在这里统一回填单位信息
            For Each node As SwcNode In Nodes
                node.Unit = unit
            Next

            For Each node As SwcNode In Nodes
                If node.Parent < 0 Then
                    Continue For
                End If

                Dim parent As SwcNode = Nothing

                If Not _index.TryGetValue(node.Parent, parent) Then
                    Call Warnings.Add($"parent {node.Parent} of node {node.Id} was not found")
                    Continue For
                End If

                If parent Is node Then
                    Call Warnings.Add($"node {node.Id} was tagged as its own parent")
                    Continue For
                End If

                node.ParentNode = parent
                Call parent.Children.Add(node)
            Next
        End Sub

        ''' <summary>
        ''' 检查骨架树的结构完整性，返回全部问题的描述文本；返回空集合表示结构有效。
        ''' </summary>
        ''' <returns>
        ''' 检查项目：根节点数量、缺失父节点的孤立节点、父子引用不一致、以及父子链上的环。
        ''' </returns>
        Public Function Validate() As String()
            Dim issues As New List(Of String)
            Dim roots As SwcNode() = Me.Roots

            If roots.Length = 0 Then
                Call issues.Add("no root node was found")
            ElseIf roots.Length > 1 Then
                Call issues.Add($"multiple root nodes: {roots.Length}")
            End If

            For Each node As SwcNode In Nodes
                If node.ParentNode Is Nothing Then
                    If node.Parent >= 0 Then
                        Call issues.Add($"node {node.Id}: parent {node.Parent} was not linked")
                    End If
                Else
                    If Not node.ParentNode.Children.Contains(node) Then
                        Call issues.Add($"node {node.Id}: parent link is not consistent")
                    End If
                End If
            Next

            ' 沿父子链向上遍历，步数超过节点总数则说明存在环
            For Each node As SwcNode In Nodes
                Dim [next] As SwcNode = node.ParentNode
                Dim steps As Integer = 0

                Do While Not [next] Is Nothing
                    steps += 1

                    If steps > Nodes.Count Then
                        Call issues.Add($"node {node.Id}: cycle was detected in the parent chain")
                        Exit Do
                    End If

                    If [next] Is node Then
                        Call issues.Add($"node {node.Id}: cycle was detected in the parent chain")
                        Exit Do
                    End If

                    [next] = [next].ParentNode
                Loop
            Next

            Return issues.ToArray
        End Function

        Public Overrides Function ToString() As String
            Dim name As String = If(EntryName, RootId.ToString)

            Return $"{name}: {Nodes.Count} nodes, {Roots.Length} root(s), unit={Unit.ToUnitName}, cable={CableLength} {Unit.ToUnitName}"
        End Function

    End Class
End Namespace
