Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports Microsoft.VisualBasic.Serialization.JSON

Namespace FAFBv783

    ''' <summary>
    ''' ``*.swc`` 神经元骨架文本文件 (SWC / neuron morphology format) 的解析器。
    ''' 
    ''' 文件的结构为「头部 ``#`` 注释行」+「节点数据行」:
    ''' 
    ''' ```
    ''' # SWC format file
    ''' # Created on 2023-11-11 using navis (https://github.com/navis-org/navis)
    ''' # Meta: {"id": "720575940590515268", "name": "None", "units": "1 nanometer"}
    ''' # PointNo Label X Y Z Radius Parent
    ''' # Labels:
    ''' # 0 = undefined, 1 = soma, 5 = fork point, 6 = end point
    ''' 1 1 191532.69 421374.75 115987.305 2238 -1
    ''' 2 0 192377.12 420970.44 116211.14 2386 10
    ''' ```
    ''' 
    ''' 节点数据行的 7 列依次是 ``n label x y z radius parent`` (以空白字符分隔)，
    ''' ``parent = -1`` 表示根节点。
    ''' </summary>
    ''' <remarks>
    ''' 数据来源：``sk_lod1_783_healed.zip`` 之中包含 13 万个 ``&lt;root_id&gt;.swc`` 文件，
    ''' 压缩包的读取封装请参考 <see cref="SkeletonArchive"/>。
    ''' </remarks>
    Public Module SwcParser

        ''' <summary>
        ''' 解析指定的 swc 文件。
        ''' </summary>
        ''' <param name="path">swc 文件所在的文件路径。</param>
        ''' <param name="encoding">文本编码，默认按照 UTF8 处理 (同时识别 BOM)。</param>
        ''' <param name="entryName">
        ''' 骨架在归档之中的条目名，用于在 ``# Meta`` 缺失的时候从文件名称之中解析 root_id；
        ''' 默认采用 <paramref name="path"/> 的文件名。
        ''' </param>
        Public Function ParseFile(path As String,
                                 Optional encoding As Encoding = Nothing,
                                 Optional entryName As String = Nothing) As SwcSkeleton

            ' 这里使用完全限定的类型名称：因为 VB 不区分大小写，参数名 path 会把 System.IO.Path 遮蔽掉
            Using stream As Stream = System.IO.File.OpenRead(path)
                Return Parse(stream, If(entryName, System.IO.Path.GetFileName(path)), encoding)
            End Using
        End Function

        ''' <summary>
        ''' 解析目标 swc 文本。
        ''' </summary>
        ''' <param name="text">swc 文件的完整文本内容。</param>
        ''' <param name="entryName">骨架在归档之中的条目名，用于解析 root_id。</param>
        Public Function ParseText(text As String, Optional entryName As String = Nothing) As SwcSkeleton
            If text Is Nothing Then
                Return New SwcSkeleton With {.EntryName = entryName}
            End If

            Return ParseLines(
                text.Split(New Char() {ControlChars.Cr, ControlChars.Lf}, StringSplitOptions.None),
                entryName
            )
        End Function

        ''' <summary>
        ''' 从数据流之中解析 swc 骨架 (例如从 zip 条目之中直接读取)。
        ''' </summary>
        ''' <param name="stream">swc 数据流。</param>
        ''' <param name="entryName">骨架在归档之中的条目名，用于解析 root_id。</param>
        ''' <param name="encoding">文本编码，默认按照 UTF8 处理 (同时识别 BOM)。</param>
        ''' <param name="leaveOpen">解析结束之后是否保持数据流为打开状态，默认关闭。</param>
        Public Function Parse(stream As Stream,
                              Optional entryName As String = Nothing,
                              Optional encoding As Encoding = Nothing,
                              Optional leaveOpen As Boolean = False) As SwcSkeleton

            Using reader As New StreamReader(stream,
                                            If(encoding, Encoding.UTF8),
                                            detectEncodingFromByteOrderMarks:=True,
                                            bufferSize:=64 * 1024,
                                            leaveOpen:=leaveOpen)

                ' 这里向 ParseLines 传递的是惰性的文本行序列，ParseLines 会在当前的 Using 作用域
                ' 之内完整的消费掉这个序列，因此不会出现「流已经关闭但序列还没有被读取」的情况
                Return ParseLines(readLines(reader), entryName)
            End Using
        End Function

        ''' <summary>
        ''' 从文本行的序列之中解析 swc 骨架。
        ''' </summary>
        ''' <param name="lines">swc 文件的文本行序列。</param>
        ''' <param name="entryName">骨架在归档之中的条目名，用于解析 root_id。</param>
        Public Function ParseLines(lines As IEnumerable(Of String), Optional entryName As String = Nothing) As SwcSkeleton
            Dim skeleton As New SwcSkeleton With {.EntryName = entryName}
            Dim inLabelsSection As Boolean = False
            Dim lineNumber As Integer = 0

            For Each line As String In lines
                lineNumber += 1

                Dim raw As String = If(line, "")

                ' 去掉可能存在的 UTF8 BOM
                If lineNumber = 1 AndAlso raw.Length > 0 AndAlso raw(0) = ChrW(&HFEFF) Then
                    raw = raw.Substring(1)
                End If

                Dim text As String = raw.Trim

                If text.Length = 0 Then
                    Continue For
                End If

                If text(0) = "#"c Then
                    ' # Labels: 段: 紧跟在 Labels 声明之后的若干行标签定义，例如
                    ' # 0 = undefined, 1 = soma, 5 = fork point, 6 = end point
                    If inLabelsSection AndAlso tryParseLabels(text.Substring(1).Trim, skeleton.Labels) Then
                        Continue For
                    End If

                    inLabelsSection = False

                    Dim comment As String = text.Substring(1).Trim

                    If comment.StartsWith("Labels:", StringComparison.OrdinalIgnoreCase) Then
                        inLabelsSection = True
                    ElseIf comment.StartsWith("Meta:", StringComparison.OrdinalIgnoreCase) Then
                        Call parseMeta(skeleton, comment.Substring("Meta:".Length).Trim)
                    End If

                    Call skeleton.Comments.Add(text)
                Else
                    Call skeleton.AddNode(parseNode(text, skeleton, lineNumber))
                End If
            Next

            ' root_id 优先采用 # Meta 之中的 id，其次是归档案目的文件名
            Dim rootId As Long

            If Not skeleton.Meta Is Nothing AndAlso tryParseRootId(skeleton.Meta.id, rootId) Then
                skeleton.RootId = rootId
            ElseIf Not String.IsNullOrEmpty(entryName) AndAlso
                tryParseRootId(System.IO.Path.GetFileNameWithoutExtension(entryName), rootId) Then

                skeleton.RootId = rootId
            End If

            ' 第二遍：在全部的节点都已经读取完毕之后再建立父子关系
            Call skeleton.BuildTree()

            Return skeleton
        End Function

        ''' <summary>
        ''' 读取数据流之中的全部文本行 (惰性迭代)。
        ''' </summary>
        Private Iterator Function readLines(reader As StreamReader) As IEnumerable(Of String)
            Dim line As String = reader.ReadLine

            Do While line IsNot Nothing
                Yield line
                line = reader.ReadLine
            Loop
        End Function

        ''' <summary>
        ''' 解析 ``# Meta: {...}`` 行。
        ''' </summary>
        Private Sub parseMeta(skeleton As SwcSkeleton, json As String)
            Dim meta As SwcMeta = Nothing
            Dim [error] As Exception = Nothing

            Try
                ' 解析失败的时候返回 Nothing，在这里进行容错而不是抛出异常
                meta = json.LoadJSON(Of SwcMeta)(simpleDict:=True, throwEx:=False, exception:=[error])
            Catch ex As Exception
                meta = Nothing
            End Try

            If meta Is Nothing Then
                ' 无法通过 json 反序列化的时候，退化为从文本之中直接提取字段值
                meta = New SwcMeta
            End If

            If String.IsNullOrEmpty(meta.id) Then
                meta.id = extractJsonValue(json, "id")
            End If
            If String.IsNullOrEmpty(meta.name) Then
                meta.name = extractJsonValue(json, "name")
            End If
            If String.IsNullOrEmpty(meta.units) Then
                meta.units = extractJsonValue(json, "units")
            End If

            skeleton.Meta = meta

            If Not [error] Is Nothing Then
                Call skeleton.Warnings.Add($"# Meta json: {[error].Message}")
            End If
        End Sub

        ''' <summary>
        ''' 从 json 文本之中提取字符串字段的值 (用于 json 反序列化失败时的容错)。
        ''' </summary>
        Private Function extractJsonValue(json As String, key As String) As String
            Dim pattern As String = """" & Regex.Escape(key) & """\s*:\s*""([^""]*)"""
            Dim match As Match = Regex.Match(json, pattern, RegexOptions.IgnoreCase)

            If match.Success Then
                Return match.Groups(1).Value
            Else
                Return Nothing
            End If
        End Function

        ''' <summary>
        ''' 解析 ``# Labels:`` 段之中一行 ``值 = 名称`` 形式的标签定义。
        ''' </summary>
        ''' <returns>是否解析出至少一个标签定义？</returns>
        Private Function tryParseLabels(comment As String, labels As Dictionary(Of Integer, String)) As Boolean
            Dim parsed As Integer = 0

            For Each item As String In comment.Split(","c)
                Dim parts As String() = item.Split("="c)

                If parts.Length <> 2 Then
                    Continue For
                End If

                Dim value As Integer

                If Not Integer.TryParse(parts(0).Trim, value) Then
                    Continue For
                End If

                If Not labels.ContainsKey(value) Then
                    ' 重复的定义保留第一次出现的名称
                    Call labels.Add(value, parts(1).Trim)
                End If

                parsed += 1
            Next

            Return parsed > 0
        End Function

        ''' <summary>
        ''' 解析一行节点数据：``n label x y z radius parent``。
        ''' </summary>
        Private Function parseNode(text As String, skeleton As SwcSkeleton, lineNumber As Integer) As SwcNode
            Dim parts As String() = text.Split(New Char() {" "c, ControlChars.Tab}, StringSplitOptions.RemoveEmptyEntries)

            ' 标准的 SWC 记录为 7 列，这里要求至少 6 列 (id/label/x/y/z/radius)
            If parts.Length < 6 Then
                Call skeleton.Warnings.Add($"line {lineNumber}: invalid node record, expected at least 6 columns: {text}")

                Return Nothing
            End If

            Dim node As New SwcNode
            Dim x As Double, y As Double, z As Double, radius As Double

            If Not Integer.TryParse(parts(0), node.Id) Then
                Call skeleton.Warnings.Add($"line {lineNumber}: invalid node id: {parts(0)}")

                Return Nothing
            End If

            If Not Integer.TryParse(parts(1), node.Label) Then
                Call skeleton.Warnings.Add($"line {lineNumber}: invalid node label: {parts(1)}")
                node.Label = 0
            End If

            ' 坐标与半径使用不变文化 (invariant culture) 解析，避免系统区域设置所导致的解析错误
            If Not tryParseDouble(parts(2), x) OrElse
                Not tryParseDouble(parts(3), y) OrElse
                Not tryParseDouble(parts(4), z) OrElse
                Not tryParseDouble(parts(5), radius) Then

                Call skeleton.Warnings.Add($"line {lineNumber}: invalid node coordinates: {text}")

                Return Nothing
            End If

            node.X = x
            node.Y = y
            node.Z = z
            node.Radius = radius

            ' 第 7 列为父节点编号，缺失的时候按照根节点处理
            If parts.Length < 7 OrElse Not Integer.TryParse(parts(6), node.Parent) Then
                node.Parent = -1
            End If

            Return node
        End Function

        Private Function tryParseDouble(text As String, ByRef value As Double) As Boolean
            Return Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, value)
        End Function

        ''' <summary>
        ''' 精确解析 FlyWire 的 root_id (避免默认的 Double 中转所导致的 Int64 精度丢失)。
        ''' </summary>
        Private Function tryParseRootId(text As String, ByRef value As Long) As Boolean
            If String.IsNullOrWhiteSpace(text) Then
                Return False
            End If

            Dim parser As New Int64Parser
            Dim parse As Object = parser.TryParse(text)

            value = If(parse Is Nothing, 0L, CLng(parse))

            Return value > 0
        End Function

    End Module
End Namespace
