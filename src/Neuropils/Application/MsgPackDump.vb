Imports System.IO
Imports System.Text
Imports FlywireAI.FAFBv783
Imports Neuropils.Data

Namespace AppLogics

    ''' <summary>
    ''' ``Neuropils.exe --dump &lt;zip&gt; [数据目录] [verify]``
    ''' </summary>
    ''' <remarks>
    ''' 把 FAFB v783 数据源里的每张表<b>分别</b>转储成 msgpack，再打包成一个 zip。
    ''' 之后界面 (<see cref="BrainDatasetLoader"/>) 直接从这个包读定型数组，
    ''' 不再去解析 261 MB 的连接表与 85 MB 的脑区表。
    ''' 
    ''' 第三个参数给 <c>verify</c> 时会多做一件事：<b>把 csv 路径与 msgpack 路径各加载一遍
    ''' 并逐项比对</b> —— 转储只能是"另一种表示"，不允许改变任何一个数字。
    ''' 转储本身是一次性的（几分钟），比对才是这个模式存在的理由。
    ''' </remarks>
    Module MsgPackDump

        Public Sub runDump(args As String())
            Dim report As New StringBuilder()
            Dim failures As Integer = 0

            Dim zipFile As String = args(2)
            Dim config As New VisualizationConfig()

            config.DataDir = If(args.Length > 3 AndAlso args(3).Length > 0, args(3), config.DataDir)

            Dim verify As Boolean = args.Length > 4 AndAlso
                                    String.Equals(args(4), "verify", StringComparison.OrdinalIgnoreCase)

            Call report.AppendLine("Neuropils msgpack dump")
            Call report.AppendLine($"data dir : {config.DataDir}")
            Call report.AppendLine($"zip file : {zipFile}")
            Call report.AppendLine($"verify   : {verify}")
            Call report.AppendLine()

            Try
                Dim clock As Stopwatch = Stopwatch.StartNew()
                Dim manifest As FafbPackManifest = FafbMsgPackStorage.Dump(
                    config,
                    zipFile,
                    Sub(message)
                        Call report.AppendLine($"      [{clock.ElapsedMilliseconds,7:N0} ms] {message}")
                        Call Console.Out.WriteLine(message)
                        Call Console.Out.Flush()
                    End Sub)

                clock.Stop()

                Call report.AppendLine()
                Call report.AppendLine($"      {manifest.Describe()}")
                Call report.AppendLine()

                If Not File.Exists(zipFile) Then
                    Call report.AppendLine("[FAIL] 转储包没有生成")

                    failures += 1
                Else
                    Call report.AppendLine($"      转储包大小: {New FileInfo(zipFile).Length / 1024 / 1024:F1} MB")
                End If

                If verify Then
                    Call verifyAgainstCsv(report, failures, config, zipFile)
                End If
            Catch ex As Exception
                Call report.AppendLine()
                Call report.AppendLine($"[FATAL] {ex.GetType().Name}: {ex.Message}")
                Call report.AppendLine(ex.StackTrace)

                Dim inner As Exception = ex.InnerException

                While inner IsNot Nothing
                    Call report.AppendLine($"[CAUSE] {inner.GetType().Name}: {inner.Message}")
                    inner = inner.InnerException
                End While

                failures += 1
            End Try

            Call report.AppendLine()
            Call report.AppendLine($"result: {(If(failures = 0, "PASS", $"FAIL ({failures})"))}")

            Dim text As String = report.ToString()

            Call Console.Out.Write(text)
            Call Console.Out.Flush()

            Try
                Call File.WriteAllText(Path.ChangeExtension(zipFile, ".dump-report.txt"), text, New UTF8Encoding(False))
            Catch
                ' 报告写不出去不影响命令行结论
            End Try

            Call Environment.Exit(If(failures = 0, 0, 1))
        End Sub

        ''' <summary>两条加载路径各跑一遍并逐项比对。</summary>
        Private Sub verifyAgainstCsv(report As StringBuilder, ByRef failures As Integer,
                                     config As VisualizationConfig, zipFile As String)
            Dim echo As Action(Of String) = Sub(message) Call report.AppendLine($"      {message}")

            Call report.AppendLine("      ---- 从 msgpack 转储包加载 ----")

            Dim packClock As Stopwatch = Stopwatch.StartNew()
            Dim fromPack As BrainDataset = New BrainDatasetLoader(config) With {
                .PreferMsgPack = True,
                .PackFile = zipFile
            }.Load(echo)

            packClock.Stop()

            Call report.AppendLine("      ---- 从 csv 加载 ----")

            Dim csvClock As Stopwatch = Stopwatch.StartNew()
            Dim fromCsv As BrainDataset = New BrainDatasetLoader(config) With {
                .PreferMsgPack = False
            }.Load(echo)

            csvClock.Stop()

            Call report.AppendLine()
            Call report.AppendLine($"      msgpack: {packClock.ElapsedMilliseconds} ms")
            Call report.AppendLine($"      csv    : {csvClock.ElapsedMilliseconds} ms")
            Call report.AppendLine()

            Call check(report, failures, "神经元数量一致", fromPack.Units, fromCsv.Units)
            Call check(report, failures, "连接条数一致", fromPack.ConnectionCount, fromCsv.ConnectionCount)

            Call compare(report, failures, "坐标", fromPack.Positions, fromCsv.Positions)
            Call compare(report, failures, "坐标标记数", fromPack.PositionMarks, fromCsv.PositionMarks)
            Call compare(report, failures, "主导脑区", fromPack.Neuropil, fromCsv.Neuropil)
            Call compare(report, failures, "主导脑区突触数", fromPack.NeuropilSynapses, fromCsv.NeuropilSynapses)
            Call compare(report, failures, "突触前", fromPack.Pre, fromCsv.Pre)
            Call compare(report, failures, "突触后", fromPack.Post, fromCsv.Post)
            Call compare(report, failures, "突触数", fromPack.SynCount, fromCsv.SynCount)
            Call compare(report, failures, "连接脑区", fromPack.ConnectionNeuropil, fromCsv.ConnectionNeuropil)
            Call compare(report, failures, "连接递质", fromPack.ConnectionNtType, fromCsv.ConnectionNtType)

            Call compare(report, failures, "脑区名表", fromPack.NeuropilNames, fromCsv.NeuropilNames)
            Call compare(report, failures, "递质类型", fromPack.Neurotransmitters, fromCsv.Neurotransmitters)
            Call compare(report, failures, "连接脑区名表", fromPack.ConnectionNeuropils, fromCsv.ConnectionNeuropils)
            Call compare(report, failures, "连接递质名表", fromPack.ConnectionNeurotransmitters, fromCsv.ConnectionNeurotransmitters)
        End Sub

#Region "比对工具"

        Private Sub check(report As StringBuilder, ByRef failures As Integer, name As String, actual As Object, expected As Object)
            Dim ok As Boolean = Equals(actual, expected)

            If Not ok Then failures += 1

            Call report.AppendLine($"      [{(If(ok, "OK", "FAIL"))}] {name}: actual={actual}, expected={expected}")
        End Sub

        Private Sub compare(report As StringBuilder, ByRef failures As Integer, name As String, a As Integer(), b As Integer())
            Dim diff As Integer = countDiff(a, b)

            If diff > 0 Then failures += 1

            Call report.AppendLine($"      [{(If(diff = 0, "OK", "FAIL"))}] {name}: {diff} 处不一致 " &
                                   $"(长度 {length(a)} vs {length(b)})")
        End Sub

        Private Sub compare(report As StringBuilder, ByRef failures As Integer, name As String, a As Double(), b As Double())
            Dim diff As Integer = countDiff(a, b)

            If diff > 0 Then failures += 1

            Call report.AppendLine($"      [{(If(diff = 0, "OK", "FAIL"))}] {name}: {diff} 处不一致 " &
                                   $"(长度 {length(a)} vs {length(b)})")
        End Sub

        Private Sub compare(report As StringBuilder, ByRef failures As Integer, name As String, a As String(), b As String())
            Dim diff As Integer = 0
            Dim count As Integer = Math.Max(length(a), length(b))

            For i As Integer = 0 To count - 1
                If Not String.Equals(item(a, i), item(b, i), StringComparison.Ordinal) Then
                    diff += 1
                End If
            Next

            If diff > 0 Then failures += 1

            Call report.AppendLine($"      [{(If(diff = 0, "OK", "FAIL"))}] {name}: {diff} 处不一致 " &
                                   $"(长度 {length(a)} vs {length(b)})")
        End Sub

        Private Function countDiff(a As Integer(), b As Integer()) As Integer
            Dim diff As Integer = 0
            Dim count As Integer = Math.Min(length(a), length(b))

            For i As Integer = 0 To count - 1
                If a(i) <> b(i) Then diff += 1
            Next

            ' 长度不同本身就是不一致
            diff += Math.Abs(length(a) - length(b))

            Return diff
        End Function

        Private Function countDiff(a As Double(), b As Double()) As Integer
            Dim diff As Integer = 0
            Dim count As Integer = Math.Min(length(a), length(b))

            For i As Integer = 0 To count - 1
                ' NaN 与 NaN 视为相等（没有坐标的神经元两侧都是 NaN）
                If Double.IsNaN(a(i)) AndAlso Double.IsNaN(b(i)) Then Continue For
                If a(i) <> b(i) Then diff += 1
            Next

            diff += Math.Abs(length(a) - length(b))

            Return diff
        End Function

        Private Function length(a As Array) As Integer
            Return If(a Is Nothing, 0, a.Length)
        End Function

        Private Function item(a As String(), i As Integer) As String
            If a Is Nothing OrElse i < 0 OrElse i >= a.Length Then Return Nothing

            Return a(i)
        End Function

#End Region

    End Module

End Namespace
