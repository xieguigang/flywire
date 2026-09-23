Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text
Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D
Imports Microsoft.VisualBasic.Imaging.Drawing3D
Imports Neuropils.Data
Imports Neuropils.Rendering

Partial Public Class FormMain

    ''' <summary>
    ''' 自检模式 (``Neuropils.exe --selftest [报告文件] [数据目录]``)。
    ''' </summary>
    ''' <remarks>
    ''' 用真实数据把"读表 → 着色 → 装配 → 拾取"整条链路跑一遍，并把每一步的<b>实测耗时</b>
    ''' 与关键规模写入报告。设计目的有两个：
    ''' 
    ''' 1. 无人值守的回归 —— 三维界面本身很难自动断言，但数据链路可以；
    ''' 2. 性能基线 —— 534 万条连接的过滤/连线构建、13 万点的投影与拾取都必须保持在
    '''    可交互的量级 (报告里的毫秒数就是验收依据)。
    ''' 
    ''' 报告里的每一条 ``[OK]`` / ``[FAIL]`` 都是硬断言，失败会以退出码 1 结束进程。
    ''' </remarks>
    Private Sub runSelfTest(args As String())
        Dim report As New StringBuilder()
        Dim failures As Integer = 0

        Dim reportFile As String = If(args.Length > 2, args(2), Path.Combine(AppContext.BaseDirectory, "selftest-report.txt"))

        m_config.DataDir = If(args.Length > 3, args(3), m_config.DataDir)

        Call report.AppendLine("Neuropils self test")
        Call report.AppendLine($"data dir : {m_config.DataDir}")
        Call report.AppendLine($"time     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
        Call report.AppendLine()

        Try
            Call appendSection(report, "1. dataset loading")

            Dim loadTimer As Stopwatch = Stopwatch.StartNew()
            Dim loader As New BrainDatasetLoader(m_config)
            Dim dataset As BrainDataset = loader.Load(Sub(message) Call report.AppendLine($"      {message}"))

            loadTimer.Stop()

            Call check(report, failures, "neuron count == 139255", dataset.Units, 139255)
            Call check(report, failures, "positioned neurons > 100000", dataset.PositionedCount > 100000, True)
            Call check(report, failures, "neuropil regions between 50 and 120",
                       dataset.NeuropilNames.Length >= 50 AndAlso dataset.NeuropilNames.Length <= 120, True)
            Call check(report, failures, "connections >= 5000000", dataset.ConnectionCount >= 5000000, True)
            Call check(report, failures, "every connection endpoint is indexed",
                       connectionsResolved(dataset), True)

            Call report.AppendLine($"      load time: {loadTimer.ElapsedMilliseconds} ms")
            Call report.AppendLine($"      {dataset}")
            Call report.AppendLine($"      neuropil assignment coverage: {coverage(dataset.Neuropil)}%")
            Call report.AppendLine($"      activity table: {If(dataset.HasActivity, dataset.ActivitySource, "(none)")}")
            Call report.AppendLine()

            ' ---- 2) 四种离散着色维度 + 热力图
            Call appendSection(report, "2. coloring dimensions")

            For Each dimension As NeuronColorDimension In {
                    NeuronColorDimension.Neuropil,
                    NeuronColorDimension.Neurotransmitter,
                    NeuronColorDimension.CellType,
                    NeuronColorDimension.SuperClass}

                Dim timer As Stopwatch = Stopwatch.StartNew()
                Dim colorizer As NeuronColorizer = NeuronColorizer.Create(dataset, dimension)
                Dim mapping As Integer() = Nothing
                Dim points As PointCloudPoint() = BrainSceneBuilder.BuildPoints(dataset, colorizer, mapping)

                timer.Stop()

                Call check(report, failures, $"[{dimension}] colorizer is available", colorizer IsNot Nothing, True)
                Call check(report, failures, $"[{dimension}] legend items within 1..24",
                           colorizer.Legend.Length >= 1 AndAlso colorizer.Legend.Length <= NeuronColorizer.DefaultMaxLegendItems, True)
                Call check(report, failures, $"[{dimension}] every category has a color text",
                           colorizer.Legend.All(Function(item) Not String.IsNullOrEmpty(colorizer.GetColorText(categorySample(dataset, colorizer, item)))), True)
                Call check(report, failures, $"[{dimension}] points match the visible positioned neurons",
                           points.Length, countVisiblePositioned(dataset, colorizer))
                Call check(report, failures, $"[{dimension}] the point -> neuron mapping stays inside the range",
                           mapping.All(Function(neuron) neuron >= 0 AndAlso neuron < dataset.Units), True)

                Call report.AppendLine($"      {dimension,-16}: {points.Length} points, {colorizer.Legend.Length} legend items, " &
                                       $"{timer.ElapsedMilliseconds} ms")
                Call report.AppendLine($"        top: {String.Join(", ", colorizer.Legend.Take(4).Select(Function(item) $"{item.Key}={item.Count}"))}")
            Next

            ' 活跃度维度：只有数据集里带 snn-output 的 neuron_activity_*.csv 时才可校验
            If dataset.HasActivity Then
                Dim heat As NeuronColorizer = NeuronColorizer.Create(dataset, NeuronColorDimension.Activity)

                Call check(report, failures, "[Activity] heat map colorizer", heat IsNot Nothing AndAlso heat.IsHeatMap, True)
                Call check(report, failures, "[Activity] intensity covers every neuron",
                           heat.Intensity IsNot Nothing AndAlso heat.Intensity.Length = dataset.Units, True)

                Dim mapping As Integer() = Nothing
                Dim points As PointCloudPoint() = BrainSceneBuilder.BuildPoints(dataset, heat, mapping)

                Call check(report, failures, "[Activity] heat map keeps every point (没有按类别过滤)",
                           points.Length, dataset.PositionedCount)
                Call report.AppendLine($"      Activity        : {points.Length} points (heat map), source={dataset.ActivitySource}")
            Else
                Call report.AppendLine("      Activity        : skipped (dataset has no neuron_activity_*.csv)")
            End If

            Call report.AppendLine()

            ' ---- 3) 连线装配 (逐条 / 阈值 / 脑区宏连接 / 选中神经元)
            Call appendSection(report, "3. connection assembly")

            Dim colorizer2 As NeuronColorizer = NeuronColorizer.Create(dataset, NeuronColorDimension.Neuropil)

            For Each threshold As Integer In {20, 100, 1000}
                Dim options As New SceneBuildOptions With {.SynapseThreshold = threshold, .MaxLines = 3000000}
                Dim timer As Stopwatch = Stopwatch.StartNew()
                Dim built = BrainSceneBuilder.BuildLines(dataset, colorizer2, options)

                timer.Stop()

                Call check(report, failures, $"[threshold {threshold}] lines <= max", built.Lines.Length <= options.MaxLines, True)
                Call report.AppendLine($"      threshold>={threshold,-5}: {built.Lines.Length,8} lines " &
                                       $"(candidates {built.Candidates}, dropped {built.Dropped}), {timer.ElapsedMilliseconds} ms")
            Next

            Dim aggregate As New SceneBuildOptions With {
                .Mode = ConnectionRenderMode.NeuropilAggregate,
                .AggregateMinStrength = 2000
            }
            Dim aggregateTimer As Stopwatch = Stopwatch.StartNew()
            Dim aggregateLines = BrainSceneBuilder.BuildLines(dataset, colorizer2, aggregate)

            aggregateTimer.Stop()

            Call check(report, failures, "neuropil aggregate links > 0", aggregateLines.Lines.Length > 0, True)
            Call check(report, failures, "neuropil aggregate links < 2000 (宏观图应保持稀疏)",
                       aggregateLines.Links < 2000, True)

            Call report.AppendLine($"      neuropil aggregate : {aggregateLines.Lines.Length} links " &
                                   $"({aggregateLines.Candidates} connections aggregated), {aggregateTimer.ElapsedMilliseconds} ms")

            ' 选中一个真实神经元，检查它的连接能否被单独装配
            Dim busiest As Integer = busiestNeuron(dataset)

            If busiest >= 0 Then
                Dim focus As New SceneBuildOptions With {
                    .Mode = ConnectionRenderMode.SelectedNeuron,
                    .FocusNeuron = busiest
                }
                Dim focusTimer As Stopwatch = Stopwatch.StartNew()
                Dim focusLines = BrainSceneBuilder.BuildLines(dataset, colorizer2, focus)

                focusTimer.Stop()

                Call check(report, failures, "selected neuron has connections", focusLines.Lines.Length > 0, True)
                Call report.AppendLine($"      focus neuron #{busiest} : {focusLines.Lines.Length} lines, {focusTimer.ElapsedMilliseconds} ms")
            End If

            Call report.AppendLine()

            ' ---- 4) 渲染管线 (DXApi) 的冒烟测试：场景 + 相机 + 投影 + 拾取
            Call appendSection(report, "4. dxapi smoke test (scene + camera + hit test)")

            Dim sceneOptions As New SceneBuildOptions With {.SynapseThreshold = 1000, .MaxLines = 20000}
            Dim builtScene As BrainScene = BrainSceneBuilder.Build(dataset, colorizer2, sceneOptions)
            Dim cloud As Scene = buildSmokeScene(builtScene)

            Call check(report, failures, "scene carries the points", cloud.PointCount, builtScene.PointCount)
            Call check(report, failures, "scene carries the connections", cloud.LineCount, builtScene.LineCount)
            Call check(report, failures, "scene reports data", cloud.HasData, True)

            Dim camera As New Camera()

            cloud.FitView(camera, New Size(900, 700))

            Call check(report, failures, "fit view produced a sane view distance", camera.ViewDistance > 0, True)

            Dim probe As Integer = firstProbePoint(cloud, camera)
            Dim hit As SceneHitTest = SceneHitTest.Miss()

            If probe < 0 Then
                Call check(report, failures, "a projected probe point is inside the viewport", False, True)
            Else
                hit = SceneHitTester.HitTest(cloud, camera, probeX(cloud, camera, probe), probeY(cloud, camera, probe), 6)

                Call check(report, failures, "hit test at a projected point finds a point", hit.Kind = SceneHitKind.Point, True)
                Call check(report, failures, "hit test stays inside the pick radius", hit.Distance <= 6.0F, True)
                Call check(report, failures, "hit test index is a valid point of the cloud",
                           hit.Index >= 0 AndAlso hit.Index < builtScene.PointCount, True)

                ' 注意：拾取返回的是"屏幕上最近的点"，密集的点云里它未必就是探针点本身
                ' (相邻神经元的投影可能更靠前)，因此不能断言索引相等 —— 真正要验证的是
                ' "点索引 -> 神经元索引 -> 坐标"这条映射链自洽。
                Dim delta As Double = mappingDelta(report, dataset, builtScene, cloud)

                Call check(report, failures, "point -> neuron -> position mapping is consistent",
                           delta >= 0 AndAlso delta <= 1.0E-03, True)
                Call report.AppendLine($"      mapping max |delta|: {delta:E3} nm")
            End If

            Call report.AppendLine($"      scene              : {cloud}")
            Call report.AppendLine($"      view distance      : {camera.ViewDistance:N1}, fov={camera.FieldOfView}")
            Call report.AppendLine($"      hit test           : {hit}")
            Call report.AppendLine()

            ' ---- 5) 拾取性能 (13 万点的线性扫描)
            Call appendSection(report, "5. picking performance")

            Const rounds As Integer = 5

            Dim pickTimer As Stopwatch = Stopwatch.StartNew()
            Dim hits As Integer = 0

            For round As Integer = 1 To rounds
                Dim h As SceneHitTest = SceneHitTester.HitTest(cloud, camera, 450 + round, 350 + round, 10)

                If h.HasHit Then hits += 1
            Next

            pickTimer.Stop()

            Dim perClick As Double = pickTimer.ElapsedMilliseconds / CDbl(rounds)

            Call report.AppendLine($"{rounds} hit tests over {cloud.PointCount} points: {pickTimer.ElapsedMilliseconds} ms " &
                                   $"({perClick:F1} ms/click, {hits} hits)")
            Call check(report, failures, "one click stays interactive (< 40 ms)", perClick < 40.0, True)
            Call check(report, failures, "the pick probes found neurons", hits > 0, True)
            Call report.AppendLine()
        Catch ex As Exception
            Call report.AppendLine()
            Call report.AppendLine($"[FATAL] {ex.GetType().Name}: {ex.Message}")
            Call report.AppendLine(ex.StackTrace)

            failures += 1
        End Try

        Call report.AppendLine($"result: {(If(failures = 0, "PASS", $"FAIL ({failures})"))}")

        Dim text As String = report.ToString()

        Try
            Call File.WriteAllText(reportFile, text, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
        Catch ex As Exception
            Trace.WriteLine($"unable to write the self test report: {ex.Message}")
        End Try

        ' WinExe 在控制台里被重定向时 stdout 仍然可用，因此同时写一份到控制台
        Call Console.Out.Write(text)
        Call Console.Out.Flush()

        Call Environment.Exit(If(failures = 0, 0, 1))
    End Sub

#Region "self test helpers"

    ''' <summary>构建冒烟测试用的场景 (点云 + 连线)。</summary>
    Private Shared Function buildSmokeScene(built As BrainScene) As Scene
        Dim scene As New Scene()

        Call scene.LoadPointCloud(built.Points)
        Call scene.LoadLineSegments(built.Lines)

        Return scene
    End Function

    ''' <summary>挑一个"投影之后落回画布内"的点作为拾取探针。</summary>
    Private Shared Function firstProbePoint(scene As Scene, camera As Camera) As Integer
        For i As Integer = 0 To scene.PointCount - 1
            Dim screen As PointF

            If Not SceneHitTester.ScreenOf(camera, toPoint3D(scene, i), screen) Then
                Continue For
            End If

            If screen.X >= 0 AndAlso screen.X < camera.Screen.Width AndAlso
               screen.Y >= 0 AndAlso screen.Y < camera.Screen.Height Then
                Return i
            End If
        Next

        Return -1
    End Function

    Private Shared Function probeX(scene As Scene, camera As Camera, index As Integer) As Integer
        Dim screen As PointF

        If index < 0 Then Return camera.Screen.Width \ 2

        Call SceneHitTester.ScreenOf(camera, toPoint3D(scene, index), screen)

        Return CInt(screen.X)
    End Function

    Private Shared Function probeY(scene As Scene, camera As Camera, index As Integer) As Integer
        Dim screen As PointF

        If index < 0 Then Return camera.Screen.Height \ 2

        Call SceneHitTester.ScreenOf(camera, toPoint3D(scene, index), screen)

        Return CInt(screen.Y)
    End Function

    Private Shared Function toPoint3D(scene As Scene, index As Integer) As Point3D
        Dim p As PointCloudPoint = scene.Points(index)

        Return New Point3D(p.X, p.Y, p.Z)
    End Function

    Private Shared Sub appendSection(report As StringBuilder, title As String)
        Call report.AppendLine($"---- {title} ----")
    End Sub

    Private Shared Sub check(report As StringBuilder, ByRef failures As Integer, label As String, actual As Object, expected As Object)
        Dim ok As Boolean = String.Equals(Convert.ToString(actual), Convert.ToString(expected))

        If Not ok Then failures += 1

        Call report.AppendLine($"      {(If(ok, "[OK]  ", "[FAIL]"))} {label}: actual={actual}, expected={expected}")
    End Sub

    ''' <summary>连接表里是否所有端点都能落到神经元索引上 (加载器已过滤，这里复核)。</summary>
    Private Shared Function connectionsResolved(dataset As BrainDataset) As Boolean
        If dataset.Pre Is Nothing OrElse dataset.Post Is Nothing Then Return False

        For c As Integer = 0 To dataset.ConnectionCount - 1
            If dataset.Pre(c) < 0 OrElse dataset.Pre(c) >= dataset.Units Then Return False
            If dataset.Post(c) < 0 OrElse dataset.Post(c) >= dataset.Units Then Return False
        Next

        Return True
    End Function

    ''' <summary>某个类别里"有坐标且可见"的神经元数量。</summary>
    Private Shared Function countVisiblePositioned(dataset As BrainDataset, colorizer As NeuronColorizer) As Integer
        Dim n As Integer = 0

        For i As Integer = 0 To dataset.Units - 1
            If dataset.HasPosition(i) AndAlso colorizer.IsNeuronVisible(i) Then
                n += 1
            End If
        Next

        Return n
    End Function

    ''' <summary>
    ''' 校验"点索引 → 神经元索引 → 数据层坐标"这条映射链自洽。
    ''' </summary>
    ''' <remarks>
    ''' 场景里的点是被<b>平移过</b>的 (减去了点云质心，让模型绕自身中心旋转)，
    ''' 因此判据是 <c>数据层坐标 − 质心 == 场景点坐标</c>。
    ''' 抽样若干点即可：这张表是逐点顺序写入的，错位会立刻暴露。
    ''' </remarks>
    Private Shared Function mappingDelta(report As StringBuilder, dataset As BrainDataset, built As BrainScene, cloud As Scene) As Double
        Dim center As Point3D = cloud.Center
        Dim samples As Integer() = {0, built.PointCount \ 3, built.PointCount \ 2, built.PointCount - 1}
        Dim worst As Double = 0

        For Each k As Integer In samples
            Dim neuron As Integer = built.PointNeurons(k)

            If neuron < 0 OrElse neuron >= dataset.Units Then Return -1
            If Not dataset.HasPosition(neuron) Then Return -1

            Dim position As FlywireAI.FAFBv783.NeuronPosition = dataset.GetPosition(neuron)

            ' 必须和 <b>场景里</b> 的点比较：装配结果 (built.Points) 保存的是未平移的原始坐标，
            ' 平移是在 Scene.LoadPointCloud 内部对它自己的副本做的 (调用方的数组不受影响)。
            Dim point As PointCloudPoint = cloud.Points(k)
            Dim dx As Double = System.Math.Abs(position.X - center.X - point.X)
            Dim dy As Double = System.Math.Abs(position.Y - center.Y - point.Y)
            Dim dz As Double = System.Math.Abs(position.Z - center.Z - point.Z)

            worst = System.Math.Max(worst, System.Math.Max(dx, System.Math.Max(dy, dz)))

            Call report.AppendLine($"        probe k={k} neuron={neuron} " &
                                   $"dataset.X={position.X:N3} center.X={center.X:N3} point.X={point.X:N3} " &
                                   $"delta=({dx:N3}, {dy:N3}, {dz:N3})")
        Next

        Return worst
    End Function

    ''' <summary>返回图例项对应的一个神经元索引 (用于验证该类别的颜色确实存在)。</summary>
    Private Shared Function categorySample(dataset As BrainDataset, colorizer As NeuronColorizer, item As ColorLegendItem) As Integer
        For i As Integer = 0 To dataset.Units - 1
            If String.Equals(colorizer.GetCategoryName(i), item.Key, StringComparison.Ordinal) Then
                Return i
            End If
        Next

        Return 0
    End Function

    ''' <summary>
    ''' 出度最大的神经元 (用于验证"选中神经元的连接"这一模式)。
    ''' </summary>
    ''' <remarks>
    ''' 用"下标即神经元索引"的计数数组而不是字典：534 万次字典写入会慢一个数量级，
    ''' 而计数数组只有 13 万个 ``Integer``。
    ''' </remarks>
    Private Shared Function busiestNeuron(dataset As BrainDataset) As Integer
        If dataset.Pre Is Nothing Then Return -1

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

    Private Shared Function coverage(values As Integer()) As String
        If values Is Nothing Then Return "0"

        Dim assigned As Integer = 0

        For Each value As Integer In values
            If value >= 0 Then assigned += 1
        Next

        Return (assigned * 100.0 / values.Length).ToString("F1")
    End Function

#End Region

End Class
