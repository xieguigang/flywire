Imports System.Diagnostics
Imports System.IO
Imports System.Text
Imports Neuropils.Analysis
Imports Neuropils.Data

''' <summary>
''' 响应曲线的命令行出图（与窗口共用同一套数据装配与绘图代码）。
''' </summary>
''' <remarks>
''' 存在的意义有两个：把实验记录批量导出成图片；以及在无人值守的环境里验证绘图链路
''' （离屏 <c>DxGraphics</c> 画布 + <c>LinePlot</c> 注入式构造函数）。
''' </remarks>
Partial Public Class FormMain

    ''' <summary>
    ''' ``--chart &lt;png&gt; [数据目录] [记录目录|latest] [维度] [口径] [组织] [取值列表] [宽] [高]``
    ''' </summary>
    ''' <remarks>
    ''' <list type="bullet">
    '''   <item>维度：``neuropil`` / ``nt`` / ``type`` / ``super`` / ``class`` / ``group``；</item>
    '''   <item>口径：``potential``（膜电位）/ ``rate``（放电率）/ ``spike``（0-1 脉冲）；</item>
    '''   <item>组织：``one``（逐个神经元）/ ``mean``（组均值）/ ``envelope``（组均值 ± 包络）；</item>
    '''   <item>取值列表：逗号分隔的标签取值（省略 = 全部响应神经元）。</item>
    ''' </list>
    ''' </remarks>
    Private Sub runChartExport(args As String())
        Dim report As New StringBuilder()
        Dim failures As Integer = 0
        Dim png As String = args(2)

        m_config.DataDir = If(args.Length > 3 AndAlso args(3).Length > 0, args(3), DefaultDataDir)

        Dim reportDir As String = If(args.Length > 4, args(4), "")

        If String.IsNullOrWhiteSpace(reportDir) OrElse String.Equals(reportDir, "latest", StringComparison.OrdinalIgnoreCase) Then
            reportDir = StimulationArchive.Latest(Path.Combine(m_config.DataDir, "snn-output"))
        End If

        Dim dimension As NeuronLabelDimension = parseDimension(If(args.Length > 5, args(5), "neuropil"))
        Dim mode As ResponseSignalMode = parseSignal(If(args.Length > 6, args(6), "potential"))
        Dim aggregation As CurveAggregation = parseAggregation(If(args.Length > 7, args(7), "one"))
        Dim values As String() = New String() {}

        If args.Length > 8 AndAlso Not String.IsNullOrWhiteSpace(args(8)) Then
            values = args(8).Split(","c)
        End If

        ' 不能用 IsNumeric：它在本工程里同时命中了两个模块（Information / PrimitiveParser）
        Dim width As Integer = 1600
        Dim height As Integer = 900

        If args.Length > 9 Then Integer.TryParse(args(9), width)
        If args.Length > 10 Then Integer.TryParse(args(10), height)
        If width <= 0 Then width = 1600
        If height <= 0 Then height = 900

        Try
            Call report.AppendLine("Neuropils stimulation response chart export")
            Call report.AppendLine($"data dir   : {m_config.DataDir}")
            Call report.AppendLine($"record dir : {reportDir}")
            Call report.AppendLine($"canvas     : {width} x {height}")
            Call report.AppendLine()

            If String.IsNullOrWhiteSpace(reportDir) OrElse Not Directory.Exists(reportDir) Then
                Throw New DirectoryNotFoundException("没有找到任何电刺激实验记录目录（先跑一次 --stimulate 或界面刺激）")
            End If

            ' 数据集只为标签（脑区 / 递质 / 细胞类型 / 分类层级）服务；标签齐全时曲线图才有筛选意义
            Dim loader As New BrainDatasetLoader(m_config)
            Dim dataset As BrainDataset = loader.Load(Sub(message) Call report.AppendLine($"      {message}"))

            Call PlotRuntime.EnsureRegistered()

            Dim data As ResponseDataset = ResponseDataset.FromReport(reportDir, dataset)

            Call report.AppendLine()
            Call report.AppendLine($"      {data.Summary}")
            Call report.AppendLine($"      responders={data.Responders.Length:N0}, steps={data.Steps}, " &
                                   $"potential={data.HasPotential}, source={Path.GetFileName(data.Source)}")

            If data.PotentialValidated.HasValue Then
                Call report.AppendLine($"      membrane potential validated={data.PotentialValidated}, mismatches={data.PotentialMismatches}")
            End If

            Dim options As New CurveOptions With {
                .Dimension = dimension,
                .Selected = values,
                .Mode = mode,
                .Aggregation = aggregation,
                .MaxCurves = 24,
                .Window = 3
            }
            Dim description As String = ""
            Dim ok As Boolean = ResponseChartForm.RenderToFile(png, data, dataset, options, width, height, description)

            Call report.AppendLine()
            Call report.AppendLine($"      curves: {description}")
            Call report.AppendLine($"      saved : {png} ({If(File.Exists(png), (New FileInfo(png)).Length \ 1024, 0):N0} KB)")

            Call check(report, failures, "chart image written", File.Exists(png), True)
            Call check(report, failures, "chart is not empty", If(File.Exists(png), (New FileInfo(png)).Length > 4096, False), True)
            Call check(report, failures, "renderer returned success", ok, True)
        Catch ex As Exception
            Call report.AppendLine()
            Call report.AppendLine($"[FATAL] {ex.GetType().Name}: {ex.Message}")
            Call report.AppendLine(ex.StackTrace)

            failures += 1
        End Try

        Call report.AppendLine()
        Call report.AppendLine($"result: {(If(failures = 0, "PASS", $"FAIL ({failures})"))}")

        Dim text As String = report.ToString()

        Call Console.Out.Write(text)
        Call Console.Out.Flush()
        Call Trace.WriteLine(text)

        Call Environment.Exit(If(failures = 0, 0, 1))
    End Sub

    Private Shared Function parseDimension(text As String) As NeuronLabelDimension
        Select Case If(text, "").Trim.ToLowerInvariant
            Case "nt", "neurotransmitter"
                Return NeuronLabelDimension.Neurotransmitter
            Case "type", "primary_type", "celltype"
                Return NeuronLabelDimension.PrimaryType
            Case "super", "super_class"
                Return NeuronLabelDimension.SuperClass
            Case "class"
                Return NeuronLabelDimension.CellClass
            Case "group"
                Return NeuronLabelDimension.CellGroup
            Case Else
                Return NeuronLabelDimension.Neuropil
        End Select
    End Function

    Private Shared Function parseSignal(text As String) As ResponseSignalMode
        Select Case If(text, "").Trim.ToLowerInvariant
            Case "rate", "spikerate"
                Return ResponseSignalMode.SpikeRate
            Case "spike", "pulse"
                Return ResponseSignalMode.Spike
            Case Else
                Return ResponseSignalMode.MembranePotential
        End Select
    End Function

    Private Shared Function parseAggregation(text As String) As CurveAggregation
        Select Case If(text, "").Trim.ToLowerInvariant
            Case "mean", "group"
                Return CurveAggregation.GroupMean
            Case "envelope", "range"
                Return CurveAggregation.GroupEnvelope
            Case Else
                Return CurveAggregation.Individual
        End Select
    End Function

End Class
