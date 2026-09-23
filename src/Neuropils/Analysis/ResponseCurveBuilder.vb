Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports Microsoft.VisualBasic.Data.Plots
Imports Neuropils.Data

Namespace Analysis

    ''' <summary>曲线图的组织方式。</summary>
    Public Enum CurveAggregation
        ''' <summary>每个响应神经元一条曲线（数量受上限约束，最活跃的优先）</summary>
        Individual
        ''' <summary>每个选中的标签取值一条均值曲线</summary>
        GroupMean
        ''' <summary>组均值 + 上下包络（虚线）</summary>
        GroupEnvelope
    End Enum

    ''' <summary>曲线图的绘制参数（界面上的下拉框全部映射到这里）。</summary>
    Public Class CurveOptions

        ''' <summary>筛选维度。</summary>
        Public Property Dimension As NeuronLabelDimension = NeuronLabelDimension.Neuropil

        ''' <summary>选中的标签取值（空 = 不筛选）。</summary>
        Public Property Selected As String() = New String() {}

        ''' <summary>信号口径。</summary>
        Public Property Mode As ResponseSignalMode = ResponseSignalMode.MembranePotential

        ''' <summary>组织方式。</summary>
        Public Property Aggregation As CurveAggregation = CurveAggregation.Individual

        ''' <summary>逐神经元模式下的曲线数量上限。</summary>
        Public Property MaxCurves As Integer = 24

        ''' <summary>放电率的滑动窗宽（步）。</summary>
        Public Property Window As Integer = 3

    End Class

    ''' <summary>
    ''' 把响应矩阵装配成可绘制的曲线集合。
    ''' </summary>
    ''' <remarks>
    ''' <b>为什么必须要"组织方式"这一层</b>：一次 140 μm 的电刺激会让三万多神经元发放，
    ''' 逐条画出来既看不清也没有意义（线宽 1 px，屏幕只有一千多像素宽）。
    ''' 因此提供三种视角：
    ''' <list type="bullet">
    '''   <item><see cref="CurveAggregation.Individual"/>：挑最活跃的若干条画出来，
    '''         用来看"单个神经元的响应波形"；</item>
    '''   <item><see cref="CurveAggregation.GroupMean"/>：每个选中的标签取值一条均值曲线，
    '''         用来看"不同脑区 / 递质 / 细胞类型的响应差异"；</item>
    '''   <item><see cref="CurveAggregation.GroupEnvelope"/>：均值曲线 + 上下包络，
    '''         用来判断"这一群神经元是同步响应还是各响各的"。</item>
    ''' </list>
    ''' </remarks>
    Public NotInheritable Class ResponseCurveBuilder

        ''' <summary>装配曲线集合（返回的列表顺序即图例顺序）。</summary>
        Public Shared Function Build(data As ResponseDataset,
                                     dataset As BrainDataset,
                                     options As CurveOptions,
                                     ByRef description As String) As List(Of Series)

            If data Is Nothing OrElse options Is Nothing Then
                description = "没有数据"

                Return New List(Of Series)()
            End If

            Dim rows As Integer() = data.SelectRows(options.Dimension, dataset, options.Selected)

            If rows.Length = 0 Then
                description = "筛选后没有任何响应神经元"

                Return New List(Of Series)()
            End If

            Select Case options.Aggregation
                Case CurveAggregation.GroupMean, CurveAggregation.GroupEnvelope
                    Return buildGroups(data, dataset, options, rows, description)
                Case Else
                    Return buildIndividual(data, options, rows, description)
            End Select
        End Function

        ''' <summary>逐神经元曲线（最活跃的优先，受上限约束）。</summary>
        Private Shared Function buildIndividual(data As ResponseDataset,
                                                options As CurveOptions,
                                                rows As Integer(),
                                                ByRef description As String) As List(Of Series)

            Dim selected As Integer() = data.TopRows(rows, options.MaxCurves)
            Dim series As New List(Of Series)(selected.Length)

            For i As Integer = 0 To selected.Length - 1
                Dim neuron As Integer = data.Responders(selected(i))

                Call series.Add(New Series With {
                    .Name = $"#{neuron} ({data.TotalSpikes(neuron):N0} spikes)",
                    .X = timeAxis(data.Steps),
                    .Y = data.Curve(selected(i), options.Mode, options.Window)
                })
            Next

            description = $"逐个神经元：{selected.Length} / {rows.Length} 条曲线" &
                          If(rows.Length > selected.Length, $"（按累计脉冲数取最活跃的 {options.MaxCurves} 个）", "")

            Return series
        End Function

        ''' <summary>按标签取值聚合的均值（± 包络）曲线。</summary>
        Private Shared Function buildGroups(data As ResponseDataset,
                                            dataset As BrainDataset,
                                            options As CurveOptions,
                                            rows As Integer(),
                                            ByRef description As String) As List(Of Series)

            ' 分组 = 选中的标签取值；没选任何值时退化成"每个取值一条曲线"
            Dim groups As New Dictionary(Of String, List(Of Integer))(StringComparer.Ordinal)
            Dim ordered As New List(Of String)()

            For i As Integer = 0 To rows.Length - 1
                Dim name As String = data.LabelOf(options.Dimension, data.Responders(rows(i)), dataset)
                Dim bucket As List(Of Integer) = Nothing

                If Not groups.TryGetValue(name, bucket) Then
                    bucket = New List(Of Integer)()
                    groups(name) = bucket
                    Call ordered.Add(name)
                End If

                Call bucket.Add(rows(i))
            Next

            Call ordered.Sort(StringComparer.Ordinal)

            Dim series As New List(Of Series)()
            Dim x As Double() = timeAxis(data.Steps)

            For i As Integer = 0 To ordered.Count - 1
                Dim bucket As List(Of Integer) = groups(ordered(i))
                Dim mean As Double() = New Double(data.Steps - 1) {}
                Dim upper As Double() = New Double(data.Steps - 1) {}
                Dim lower As Double() = New Double(data.Steps - 1) {}

                For t As Integer = 0 To data.Steps - 1
                    Dim sum As Double = 0
                    Dim hi As Double = Double.MinValue
                    Dim lo As Double = Double.MaxValue

                    For k As Integer = 0 To bucket.Count - 1
                        Dim curve As Double() = data.Curve(bucket(k), options.Mode, options.Window)
                        Dim value As Double = If(t < curve.Length, curve(t), 0)

                        sum += value
                        If value > hi Then hi = value
                        If value < lo Then lo = value
                    Next

                    mean(t) = sum / bucket.Count
                    upper(t) = If(hi = Double.MinValue, 0, hi)
                    lower(t) = If(lo = Double.MaxValue, 0, lo)
                Next

                If options.Aggregation = CurveAggregation.GroupEnvelope Then
                    ' 包络先画（虚线），均值后画：保证均值曲线压在包络之上
                    Call series.Add(New Series With {
                        .Name = $"{ordered(i)} 上限 (n={bucket.Count})",
                        .X = x,
                        .Y = upper,
                        .LineStyle = DashStyle.Dash
                    })
                    Call series.Add(New Series With {
                        .Name = $"{ordered(i)} 下限",
                        .X = x,
                        .Y = lower,
                        .LineStyle = DashStyle.Dash
                    })
                End If

                Call series.Add(New Series With {
                    .Name = $"{ordered(i)} 均值 (n={bucket.Count})",
                    .X = x,
                    .Y = mean
                })
            Next

            description = $"{If(options.Aggregation = CurveAggregation.GroupEnvelope, "组均值 ± 包络", "组均值")}：" &
                          $"{ordered.Count} 个标签取值，覆盖 {rows.Length:N0} 个响应神经元"

            Return series
        End Function

        ''' <summary>时间轴（1 基步号，与报告里的 <c>step</c> 列一致）。</summary>
        Private Shared Function timeAxis(steps As Integer) As Double()
            Dim x As Double() = New Double(System.Math.Max(0, steps - 1)) {}

            For t As Integer = 0 To x.Length - 1
                x(t) = t + 1
            Next

            Return x
        End Function

        ''' <summary>把选中的标签取值拼成可读的一句话（放标题里）。</summary>
        Public Shared Function Describe(options As CurveOptions) As String
            Dim dimension As String = DescribeDimension(options.Dimension)
            Dim scope As String = If(options.Selected Is Nothing OrElse options.Selected.Length = 0,
                                     "全部响应神经元",
                                     $"{options.Selected.Length} 个{If(dimension, "标签")}: " & String.Join(", ", options.Selected.Take(3)) &
                                     If(options.Selected.Length > 3, " …", ""))

            Return $"{dimension} · {scope}"
        End Function

        Public Shared Function DescribeDimension(dimension As NeuronLabelDimension) As String
            Select Case dimension
                Case NeuronLabelDimension.Neuropil
                    Return "主导脑区"
                Case NeuronLabelDimension.Neurotransmitter
                    Return "神经递质"
                Case NeuronLabelDimension.PrimaryType
                    Return "细胞类型"
                Case NeuronLabelDimension.SuperClass
                    Return "分类层级 super_class"
                Case NeuronLabelDimension.CellClass
                    Return "分类层级 class"
                Case Else
                    Return "分类层级 group"
            End Select
        End Function

        Public Shared Function DescribeMode(mode As ResponseSignalMode) As String
            Select Case mode
                Case ResponseSignalMode.MembranePotential
                    Return "触发前膜电位"
                Case ResponseSignalMode.SpikeRate
                    Return "滑动窗放电率"
                Case Else
                    Return "脉冲 (0/1)"
            End Select
        End Function

    End Class

End Namespace
