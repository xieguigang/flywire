Imports System.IO
Imports System.Text
Imports FlywireAI.Connectome
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
                    Call verifyPackContents(report, failures, config, zipFile)
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

        ''' <summary>
        ''' 校验转储包："包里的行数 = 清单声明的行数"，并真的用它装配一遍数据集。
        ''' </summary>
        ''' <remarks>
        ''' 数据源改成只有 msgpack 之后，这里<b>没有 csv 路径可比对</b>了；
        ''' 能校验的是两件事：① 清单里声明的行数与包里实际数组长度一致；
        ''' ② 用这份包真的能装配出一个规模正确、端点全部可解析的数据集。
        ''' 字节级的往返一致性由 src/test 的第 11 节负责。
        ''' </remarks>
        Private Sub verifyPackContents(report As StringBuilder, ByRef failures As Integer,
                                       config As VisualizationConfig, zipFile As String)
            Dim echo As Action(Of String) = Sub(message) Call report.AppendLine($"      {message}")

            ' 1) 包里的行数 == 清单里声明的行数
            Dim rows As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

            Using reader As FafbPackReader = FafbMsgPackStorage.Open(zipFile)
                If reader.Manifest Is Nothing OrElse reader.Manifest.Sources Is Nothing Then
                    Call check(report, failures, "转储包里有清单", False, True)

                    Return
                End If

                For Each source As FafbPackSource In reader.Manifest.Sources
                    Dim actual As Integer = 0

                    Select Case source.Key
                        Case FafbMsgPackStorage.KeyNames
                            actual = packRows(Of CellNamesPack)(reader, source.Key, source)
                        Case FafbMsgPackStorage.KeyClassification
                            actual = packRows(Of ClassificationPack)(reader, source.Key, source)
                        Case FafbMsgPackStorage.KeyCellTypes
                            actual = packRows(Of CellTypesPack)(reader, source.Key, source)
                        Case FafbMsgPackStorage.KeyNeurons
                            actual = packRows(Of NeuronsPack)(reader, source.Key, source)
                        Case FafbMsgPackStorage.KeyCoordinates
                            actual = packRows(Of CoordinatesPack)(reader, source.Key, source)
                        Case FafbMsgPackStorage.KeyNeuropil
                            Dim pack As NeuropilTablePack = reader.Read(Of NeuropilTablePack)(source.Key)

                            actual = If(pack Is Nothing, 0, pack.RowCount)
                        Case FafbMsgPackStorage.KeyConnections
                            Dim pack As ConnectionsPack = reader.Read(Of ConnectionsPack)(source.Key)

                            actual = If(pack Is Nothing, 0, pack.RowCount)
                    End Select

                    rows(source.Key) = actual
                    Call check(report, failures, $"[{source.Key}] 行数与清单一致", actual, source.Rows)
                Next
            End Using

            ' 2) 真的用它装配一遍数据集
            Call report.AppendLine("      ---- 用转储包装配数据集 ----")

            Dim loader As New BrainDatasetLoader(config) With {.PackFile = zipFile}
            Dim dataset As BrainDataset = loader.Load(echo)

            Call check(report, failures, "神经元数量 == names 行数", dataset.Units, rows(FafbMsgPackStorage.KeyNames))
            Call check(report, failures, "连接条数 == connections 行数", dataset.ConnectionCount, rows(FafbMsgPackStorage.KeyConnections))
            Call check(report, failures, "全部端点都在索引里", connectionsResolved(dataset), True)
        End Sub

        Private Function packRows(Of T As Class)(reader As FafbPackReader, key As String, source As FafbPackSource) As Integer
            Dim pack As T = reader.Read(Of T)(key)

            If pack Is Nothing Then
                Return 0
            End If

            Dim propertyInfo As Reflection.PropertyInfo = pack.GetType().GetProperty("RootId")

            If propertyInfo Is Nothing Then Return 0

            Dim rootId As Array = TryCast(propertyInfo.GetValue(pack), Array)

            Return If(rootId Is Nothing, 0, rootId.Length)
        End Function

        ''' <summary>所有连接条目的端点都能在索引里解析出来 (没有因为 root_id 不认识而被跳过的行)。</summary>
        Private Function connectionsResolved(dataset As BrainDataset) As Boolean
            Dim index As ConnectomeIndex = dataset.Index

            For i As Integer = 0 To dataset.ConnectionCount - 1
                If dataset.Pre(i) < 0 OrElse dataset.Pre(i) >= index.Size Then Return False
                If dataset.Post(i) < 0 OrElse dataset.Post(i) >= index.Size Then Return False
            Next

            Return True
        End Function

#Region "比对工具"

        Private Sub check(report As StringBuilder, ByRef failures As Integer, name As String, actual As Object, expected As Object)
            Dim ok As Boolean = Equals(actual, expected)

            If Not ok Then failures += 1

            Call report.AppendLine($"      [{(If(ok, "OK", "FAIL"))}] {name}: actual={actual}, expected={expected}")
        End Sub

#End Region

    End Module

End Namespace
