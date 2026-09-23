Imports System.Drawing
Imports System.Text
Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D
Imports Microsoft.VisualBasic.Imaging.Drawing3D

Namespace Rendering

    ''' <summary>
    ''' 连接的绘制模式。
    ''' </summary>
    Public Enum ConnectionRenderMode

        ''' <summary>每条 (pre, post) 连接画一根线 (原始连接组)。</summary>
        PerConnection = 0

        ''' <summary>聚合为脑区 ↔ 脑区的宏连接 (几百根线，看清宏观结构)。</summary>
        NeuropilAggregate = 1

        ''' <summary>只画选中神经元的连接 (用于"选中一个神经元看它连到哪")。</summary>
        SelectedNeuron = 2

    End Enum

    ''' <summary>
    ''' 连线的着色方式。
    ''' </summary>
    Public Enum LineColorMode

        ''' <summary>跟随前突触神经元的颜色 (与点云配色一致，最容易对应)。</summary>
        PresynapticNeuron = 0

        ''' <summary>按连接的递质类型上色 (抑制性/兴奋性一目了然)。</summary>
        Neurotransmitter = 1

        ''' <summary>统一使用半透明灰。</summary>
        Uniform = 2

    End Enum

    ''' <summary>
    ''' 场景装配参数。
    ''' </summary>
    Public Class SceneBuildOptions

        ''' <summary>只画突触数不少于该值的连接 (界面上是一个滑块)。</summary>
        Public Property SynapseThreshold As Integer = 20

        ''' <summary>
        ''' 连线的数量上限，``&lt;= 0`` 表示不限制。
        ''' </summary>
        ''' <remarks>
        ''' 默认值高于本数据集的全量连接数 (534 万)，也就是"默认不截断"：真正控制画面密度的是
        ''' <see cref="SynapseThreshold"/> (≥20 突触时约 50 万条线)，这里只是防止显存与
        ''' 上传时间被极端参数打爆。
        ''' 显存量级：每条线 2 个顶点 × 16 字节，200 万条约 64 MB，全量约 171 MB。
        ''' </remarks>
        Public Property MaxLines As Integer = 6000000

        ''' <summary>
        ''' 连线列表的初始容量上限。
        ''' </summary>
        ''' <remarks>
        ''' 不要按"可能的最大条数"预分配：滑块把阈值从 20 提到 100 时实际只需要 2 万条线，
        ''' 却会为此预留/清零几百 MB (534 万 × 每个 LineSegment 56 字节 ≈ 300 MB)。
        ''' 这里只预留一个有代表性的大小，超出部分交给 List 自己的增长策略。
        ''' </remarks>
        Public Const LineListPresetCapacity As Integer = 200000

        ''' <summary>连线的数量是否有限制？</summary>
        Public ReadOnly Property HasLineLimit As Boolean
            Get
                Return MaxLines > 0
            End Get
        End Property

        ''' <summary>连接的绘制模式。</summary>
        Public Property Mode As ConnectionRenderMode = ConnectionRenderMode.PerConnection

        ''' <summary>连线的着色方式。</summary>
        Public Property LineColor As LineColorMode = LineColorMode.PresynapticNeuron

        ''' <summary>连线的透明度 (0~255)；连接组很密，半透明才有层次。</summary>
        Public Property LineAlpha As Integer = 110

        ''' <summary>选中神经元模式下的目标神经元索引。</summary>
        Public Property FocusNeuron As Integer = -1

        ''' <summary>宏连接模式下的最小突触总数。</summary>
        Public Property AggregateMinStrength As Integer = 2000

    End Class

    ''' <summary>
    ''' 装配好的三维场景内容。
    ''' </summary>
    Public Class BrainScene

        ''' <summary>点云。</summary>
        Public Property Points As PointCloudPoint()

        ''' <summary>
        ''' 点索引 → 神经元索引。
        ''' </summary>
        ''' <remarks>
        ''' 点云经过了"有坐标 + 通过筛选"的过滤，因此第 k 个点并不等于第 k 个神经元。
        ''' 鼠标拾取拿到的下标必须经过这张表才能查到正确的 root_id。
        ''' </remarks>
        Public Property PointNeurons As Integer()

        ''' <summary>连线。</summary>
        Public Property Lines As LineSegment()

        ''' <summary>满足阈值与可见性的连接总数 (被画出的 + 因上限丢弃的)。</summary>
        Public Property CandidateLines As Integer

        ''' <summary>因 <see cref="SceneBuildOptions.MaxLines"/> 被丢弃的连接数。</summary>
        Public Property DroppedLines As Integer

        ''' <summary>宏连接模式下的脑区级连线数。</summary>
        Public Property AggregateLinks As Integer

        Public ReadOnly Property PointCount As Integer
            Get
                Return If(Points Is Nothing, 0, Points.Length)
            End Get
        End Property

        Public ReadOnly Property LineCount As Integer
            Get
                Return If(Lines Is Nothing, 0, Lines.Length)
            End Get
        End Property

        ''' <summary>神经元索引 → 点索引 (拾取的反向映射)，未出现在点云里的神经元为 -1。</summary>
        Public Function BuildLookup(units As Integer) As Integer()
            Dim lookup As Integer() = New Integer(units - 1) {}

            For i As Integer = 0 To lookup.Length - 1
                lookup(i) = -1
            Next

            If PointNeurons Is Nothing Then Return lookup

            For k As Integer = 0 To PointNeurons.Length - 1
                lookup(PointNeurons(k)) = k
            Next

            Return lookup
        End Function

        Public Function Describe() As String
            If Lines Is Nothing OrElse Lines.Length = 0 Then
                Return $"{PointCount} points"
            End If

            Dim sb As New StringBuilder()

            Call sb.Append($"{PointCount} points, {LineCount} lines")

            If AggregateLinks > 0 Then
                Call sb.Append($" ({AggregateLinks} neuropil links)")
            End If

            If DroppedLines > 0 Then
                Call sb.Append($", {DroppedLines} lines dropped of {CandidateLines}")
            End If

            Return sb.ToString
        End Function

    End Class

    ''' <summary>
    ''' 把 <see cref="Data.BrainDataset"/> 装配成渲染管线所需的点云与连线。
    ''' </summary>
    ''' <remarks>
    ''' 装配是纯托管的数据转换（没有 GPU 调用），因此可以在后台线程上完成：
    ''' 全量连接组的过滤 + 构建约需数百毫秒，放在 UI 线程上会明显卡顿。
    ''' </remarks>
    Public Module BrainSceneBuilder

        ''' <summary>递质类型 → 连线颜色 (仅用于 <see cref="LineColorMode.Neurotransmitter"/>)。</summary>
        Private ReadOnly NtColors As New Dictionary(Of String, Color)(StringComparer.OrdinalIgnoreCase) From {
            {"GABA", Color.FromArgb(220, 60, 60)},
            {"ACH", Color.FromArgb(70, 130, 220)},
            {"GLUT", Color.FromArgb(240, 170, 60)},
            {"DA", Color.FromArgb(160, 90, 200)},
            {"SER", Color.FromArgb(60, 190, 160)},
            {"OCT", Color.FromArgb(220, 120, 170)}
        }

        Private ReadOnly NtFallback As Color = Color.FromArgb(150, 150, 150)

        ''' <summary>装配点云与连线。</summary>
        Public Function Build(dataset As Data.BrainDataset,
                              colorizer As Data.NeuronColorizer,
                              options As SceneBuildOptions) As BrainScene

            If dataset Is Nothing Then Throw New ArgumentNullException(NameOf(dataset))

            If options Is Nothing Then options = New SceneBuildOptions()

            Dim scene As New BrainScene()
            Dim points As Integer() = Nothing

            scene.Points = composePoints(dataset, colorizer, points)
            scene.PointNeurons = points

            Dim built As (Lines As LineSegment(), Candidates As Integer, Dropped As Integer, Links As Integer) =
                composeLines(dataset, colorizer, options)

            scene.Lines = built.Lines
            scene.CandidateLines = built.Candidates
            scene.DroppedLines = built.Dropped
            scene.AggregateLinks = built.Links

            Return scene
        End Function

        ''' <summary>
        ''' 出度最大的神经元。
        ''' </summary>
        ''' <remarks>
        ''' 用作"选中神经元的连接"这个模式的默认目标：全脑里最忙的那个神经元
        ''' (几万条连接) 是最能说明"网络长什么样"的样本。
        ''' 用"下标即神经元索引"的计数数组而不是字典：534 万次字典写入会慢一个数量级，
        ''' 而计数数组只有 13 万个 ``Integer``。
        ''' </remarks>
        Public Function FindBusiestNeuron(dataset As Data.BrainDataset) As Integer
            If dataset Is Nothing OrElse dataset.Pre Is Nothing Then Return -1

            Dim counts As Integer() = New Integer(dataset.Units - 1) {}
            Dim best As Integer = -1
            Dim bestCount As Integer = 0

            For c As Integer = 0 To dataset.ConnectionCount - 1
                Dim neuron As Integer = dataset.Pre(c)

                counts(neuron) += 1

                If counts(neuron) > bestCount Then
                    bestCount = counts(neuron)
                    best = neuron
                End If
            Next

            Return best
        End Function

        ''' <summary>只装配点云 (着色维度变化时不需要重建连线)。</summary>
        Public Function BuildPoints(dataset As Data.BrainDataset,
                                    colorizer As Data.NeuronColorizer,
                                    ByRef neurons As Integer()) As PointCloudPoint()

            If dataset Is Nothing Then Throw New ArgumentNullException(NameOf(dataset))

            Return composePoints(dataset, colorizer, neurons)
        End Function

        ''' <summary>只装配连线 (强度阈值 / 模式变化时不需要重建点云)。</summary>
        Public Function BuildLines(dataset As Data.BrainDataset,
                                   colorizer As Data.NeuronColorizer,
                                   options As SceneBuildOptions) As (Lines As LineSegment(), Candidates As Integer, Dropped As Integer, Links As Integer)

            Return composeLines(dataset, colorizer, options)
        End Function

#Region "points"

        ''' <remarks>
        ''' 名字不能叫 BuildPoints：VB 不区分大小写，会与上面的公开重载冲突
        ''' (同一个模块内两个只差大小写的成员是重定义错误)。
        ''' </remarks>
        Private Function composePoints(dataset As Data.BrainDataset,
                                     colorizer As Data.NeuronColorizer,
                                     ByRef neurons As Integer()) As PointCloudPoint()

            Dim units As Integer = dataset.Units
            Dim selected As New List(Of Integer)(units)

            For i As Integer = 0 To units - 1
                If Not dataset.HasPosition(i) Then
                    Continue For
                End If

                ' 图例里被隐藏的类别不进入点云：GPU 侧就不会为它上传任何顶点
                If colorizer IsNot Nothing AndAlso Not colorizer.IsNeuronVisible(i) Then
                    Continue For
                End If

                Call selected.Add(i)
            Next

            Dim count As Integer = selected.Count
            Dim points As PointCloudPoint() = New PointCloudPoint(count - 1) {}
            Dim mapping As Integer() = New Integer(count - 1) {}

            For k As Integer = 0 To count - 1
                Dim i As Integer = selected(k)
                Dim position As FlywireAI.FAFBv783.NeuronPosition = dataset.GetPosition(i)
                Dim intensity As Double = 0
                Dim color As String = Nothing

                If colorizer IsNot Nothing Then
                    If colorizer.IsHeatMap Then
                        If colorizer.Intensity IsNot Nothing AndAlso i < colorizer.Intensity.Length Then
                            intensity = colorizer.Intensity(i)
                        End If
                    Else
                        color = colorizer.GetColorText(i)
                    End If
                End If

                points(k) = New PointCloudPoint(position.X, position.Y, position.Z, intensity, color)
                mapping(k) = i
            Next

            neurons = mapping

            Return points
        End Function

#End Region

#Region "lines"

        ''' <remarks>
        ''' 名字不能叫 BuildLines：与公开重载同名 (VB 不区分大小写)。
        ''' </remarks>
        Private Function composeLines(dataset As Data.BrainDataset,
                                      colorizer As Data.NeuronColorizer,
                                      options As SceneBuildOptions) As (Lines As LineSegment(), Candidates As Integer, Dropped As Integer, Links As Integer)

            Select Case options.Mode
                Case ConnectionRenderMode.NeuropilAggregate
                    Return buildAggregatedLines(dataset, colorizer, options)
                Case ConnectionRenderMode.SelectedNeuron
                    Return buildFocusLines(dataset, colorizer, options)
                Case Else
                    Return buildPerConnectionLines(dataset, colorizer, options)
            End Select
        End Function

        ''' <summary>每条连接一根线。</summary>
        Private Function buildPerConnectionLines(dataset As Data.BrainDataset,
                                                 colorizer As Data.NeuronColorizer,
                                                 options As SceneBuildOptions) As (Lines As LineSegment(), Candidates As Integer, Dropped As Integer, Links As Integer)

            Dim total As Integer = dataset.ConnectionCount
            Dim limited As Boolean = options.HasLineLimit
            Dim capacity As Integer = System.Math.Min(total, SceneBuildOptions.LineListPresetCapacity)

            If limited Then
                capacity = System.Math.Min(capacity, System.Math.Max(1024, options.MaxLines))
            End If

            Dim lines As New List(Of LineSegment)(capacity)
            Dim pre As Integer() = dataset.Pre
            Dim post As Integer() = dataset.Post
            Dim strength As Integer() = dataset.SynCount
            Dim threshold As Integer = System.Math.Max(1, options.SynapseThreshold)
            Dim candidates As Integer = 0
            Dim dropped As Integer = 0

            For c As Integer = 0 To total - 1
                If strength(c) < threshold Then
                    Continue For
                End If

                Dim a As Integer = pre(c)
                Dim b As Integer = post(c)

                If Not dataset.HasPosition(a) OrElse Not dataset.HasPosition(b) Then
                    Continue For
                End If

                If colorizer IsNot Nothing AndAlso
                   (Not colorizer.IsNeuronVisible(a) OrElse Not colorizer.IsNeuronVisible(b)) Then
                    Continue For
                End If

                candidates += 1

                If limited AndAlso lines.Count >= options.MaxLines Then
                    dropped += 1
                    Continue For
                End If

                Call lines.Add(New LineSegment(toPoint3D(dataset.GetPosition(a)),
                                               toPoint3D(dataset.GetPosition(b)),
                                               lineColor(dataset, colorizer, options, c, a)))
            Next

            Return (lines.ToArray(), candidates, dropped, 0)
        End Function

        ''' <summary>只画某个神经元的输入/输出连接。</summary>
        Private Function buildFocusLines(dataset As Data.BrainDataset,
                                         colorizer As Data.NeuronColorizer,
                                         options As SceneBuildOptions) As (Lines As LineSegment(), Candidates As Integer, Dropped As Integer, Links As Integer)

            Dim focus As Integer = options.FocusNeuron

            If focus < 0 OrElse focus >= dataset.Units OrElse Not dataset.HasPosition(focus) Then
                Return (New LineSegment() {}, 0, 0, 0)
            End If

            Dim lines As New List(Of LineSegment)(1024)
            Dim focusPosition As Point3D = toPoint3D(dataset.GetPosition(focus))
            Dim candidates As Integer = 0
            Dim dropped As Integer = 0
            Dim limited As Boolean = options.HasLineLimit

            For c As Integer = 0 To dataset.ConnectionCount - 1
                Dim a As Integer = dataset.Pre(c)
                Dim b As Integer = dataset.Post(c)

                If a <> focus AndAlso b <> focus Then
                    Continue For
                End If

                Dim other As Integer = If(a = focus, b, a)

                If Not dataset.HasPosition(other) Then
                    Continue For
                End If

                candidates += 1

                If limited AndAlso lines.Count >= options.MaxLines Then
                    dropped += 1
                    Continue For
                End If

                ' 入边与出边用不同的颜色区分，交互时才分得清方向
                Dim color As Color

                If a = focus Then
                    color = withAlpha(Color.FromArgb(255, 120, 60), options.LineAlpha)
                Else
                    color = withAlpha(Color.FromArgb(60, 160, 255), options.LineAlpha)
                End If

                Call lines.Add(New LineSegment(focusPosition, toPoint3D(dataset.GetPosition(other)), color))
            Next

            Return (lines.ToArray(), candidates, dropped, 0)
        End Function

        ''' <summary>
        ''' 把连接组聚合为脑区 ↔ 脑区的宏连接。
        ''' </summary>
        ''' <remarks>
        ''' 端点取各脑区可见神经元的质心，颜色与透明度由"突触总数"决定
        ''' (越强的连接越不透明)，因此即使只有几百根线也能看出信息流的方向。
        ''' </remarks>
        Private Function buildAggregatedLines(dataset As Data.BrainDataset,
                                              colorizer As Data.NeuronColorizer,
                                              options As SceneBuildOptions) As (Lines As LineSegment(), Candidates As Integer, Dropped As Integer, Links As Integer)

            Dim regions As Integer = If(dataset.ConnectionNeuropils Is Nothing, 0, dataset.ConnectionNeuropils.Length)

            If regions = 0 Then
                Return (New LineSegment() {}, 0, 0, 0)
            End If

            ' 1) 每个脑区里"可见且有坐标"的神经元质心
            Dim sumX As Double() = New Double(regions - 1) {}
            Dim sumY As Double() = New Double(regions - 1) {}
            Dim sumZ As Double() = New Double(regions - 1) {}
            Dim counts As Integer() = New Integer(regions - 1) {}

            ' 连接表的脑区名与神经元的主导脑区名来自不同的表，这里按"连接所在脑区"聚合
            Dim regionOfNeuron As Integer() = New Integer(dataset.Units - 1) {}

            For i As Integer = 0 To dataset.Units - 1
                regionOfNeuron(i) = -1

                If Not dataset.HasPosition(i) Then
                    Continue For
                End If
                If colorizer IsNot Nothing AndAlso Not colorizer.IsNeuronVisible(i) Then
                    Continue For
                End If

                Dim region As Integer = regionIndexOf(dataset, dataset.GetNeuropilName(i))

                If region < 0 Then
                    Continue For
                End If

                Dim position As FlywireAI.FAFBv783.NeuronPosition = dataset.GetPosition(i)

                regionOfNeuron(i) = region
                sumX(region) += position.X
                sumY(region) += position.Y
                sumZ(region) += position.Z
                counts(region) += 1
            Next

            ' 2) 累加脑区之间的突触总数
            Dim links As New Dictionary(Of Long, Long)()
            Dim threshold As Integer = System.Math.Max(1, options.SynapseThreshold)
            Dim candidates As Integer = 0

            For c As Integer = 0 To dataset.ConnectionCount - 1
                If dataset.SynCount(c) < threshold Then
                    Continue For
                End If

                Dim a As Integer = regionOfNeuron(dataset.Pre(c))
                Dim b As Integer = regionOfNeuron(dataset.Post(c))

                If a < 0 OrElse b < 0 OrElse a = b Then
                    Continue For
                End If

                Dim key As Long = CLng(a) * 1000000L + b
                Dim total As Long = 0

                Call links.TryGetValue(key, total)

                links(key) = total + dataset.SynCount(c)
                candidates += 1
            Next

            ' 3) 生成线段
            Dim lines As New List(Of LineSegment)(links.Count)

            For Each link As KeyValuePair(Of Long, Long) In links
                If link.Value < options.AggregateMinStrength Then
                    Continue For
                End If

                Dim a As Integer = CInt(link.Key \ 1000000L)
                Dim b As Integer = CInt(link.Key Mod 1000000L)

                If counts(a) = 0 OrElse counts(b) = 0 Then
                    Continue For
                End If

                Dim from As New Point3D(sumX(a) / counts(a), sumY(a) / counts(a), sumZ(a) / counts(a))
                Dim [to] As New Point3D(sumX(b) / counts(b), sumY(b) / counts(b), sumZ(b) / counts(b))
                Dim color As Color = aggregateColor(link.Value, options)

                Call lines.Add(New LineSegment(from, [to], color))
            Next

            Return (lines.ToArray(), candidates, 0, lines.Count)
        End Function

        ''' <summary>宏连接的配色：突触总数越多越不透明、越偏暖色。</summary>
        Private Function aggregateColor(synapses As Long, options As SceneBuildOptions) As Color
            ' 以 10^6 突触为满量程 (全脑最强的一条脑区间连接约在百万级)
            Dim t As Double = System.Math.Min(1.0, synapses / 1000000.0)
            Dim alpha As Integer = CInt(System.Math.Max(40, System.Math.Min(255, options.LineAlpha + t * (255 - options.LineAlpha))))

            Return Color.FromArgb(alpha,
                                  CInt(70 + t * 185),
                                  CInt(150 - t * 90),
                                  CInt(230 - t * 200))
        End Function

#End Region

#Region "helpers"

        Private Function lineColor(dataset As Data.BrainDataset,
                                   colorizer As Data.NeuronColorizer,
                                   options As SceneBuildOptions,
                                   connection As Integer,
                                   presynaptic As Integer) As Color

            Select Case options.LineColor
                Case LineColorMode.Neurotransmitter
                    Dim name As String = dataset.GetConnectionNtTypeName(connection)
                    Dim color As Color = Nothing

                    If Not String.IsNullOrEmpty(name) AndAlso NtColors.TryGetValue(name, color) Then
                        Return withAlpha(color, options.LineAlpha)
                    End If

                    Return withAlpha(NtFallback, options.LineAlpha)

                Case LineColorMode.Uniform
                    Return withAlpha(NtFallback, options.LineAlpha)

                Case Else
                    If colorizer IsNot Nothing AndAlso Not colorizer.IsHeatMap Then
                        Dim color As Color = colorizer.GetColor(presynaptic)

                        If Not color.IsEmpty Then
                            Return withAlpha(color, options.LineAlpha)
                        End If
                    End If

                    ' 热力图点云没有"每神经元的离散颜色"，连线退回中性灰
                    Return withAlpha(NtFallback, options.LineAlpha)
            End Select
        End Function

        Private Function withAlpha(color As Color, alpha As Integer) As Color
            Return Color.FromArgb(System.Math.Max(0, System.Math.Min(255, alpha)), color)
        End Function

        Private Function toPoint3D(position As FlywireAI.FAFBv783.NeuronPosition) As Point3D
            Return New Point3D(position.X, position.Y, position.Z)
        End Function

        Private Function regionIndexOf(dataset As Data.BrainDataset, name As String) As Integer
            If dataset.ConnectionNeuropils Is Nothing Then Return -1

            For i As Integer = 0 To dataset.ConnectionNeuropils.Length - 1
                If String.Equals(dataset.ConnectionNeuropils(i), name, StringComparison.OrdinalIgnoreCase) Then
                    Return i
                End If
            Next

            Return -1
        End Function

#End Region

    End Module

End Namespace
