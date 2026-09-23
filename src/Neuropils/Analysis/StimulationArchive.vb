Imports System.IO
Imports System.Text

Namespace Analysis

    ''' <summary>
    ''' 电刺激实验记录目录的索引：每一次刺激都会在
    ''' <c>&lt;数据目录&gt;\snn-output\&lt;时间戳&gt;_stimulation\</c> 下落一份完整记录。
    ''' </summary>
    ''' <remarks>
    ''' 有了这个索引，"打开历史实验"就不必依赖会话内存 —— 关掉程序再打开，
    ''' 之前的响应结果依然可以画图。
    ''' </remarks>
    Public NotInheritable Class StimulationArchive

        ''' <summary>记录目录的后缀（由 <c>StimulationReport.Write</c> 生成）。</summary>
        Public Const Suffix As String = "_stimulation"

        ''' <summary>列出全部实验记录目录（按时间倒序，最新在前）。</summary>
        Public Shared Function Enumerate(root As String) As String()
            If String.IsNullOrWhiteSpace(root) OrElse Not Directory.Exists(root) Then
                Return New String() {}
            End If

            Dim dirs As New List(Of String)()

            ' 循环变量不能叫 path：VB 不区分大小写，会遮蔽 System.IO.Path
            For Each dir As String In Directory.EnumerateDirectories(root, "*" & Suffix)
                If File.Exists(IO.Path.Combine(dir, "stimulation_summary.csv")) Then
                    Call dirs.Add(dir)
                End If
            Next

            Dim ordered As String() = dirs.ToArray()

            System.Array.Sort(ordered, Function(a As String, b As String)
                                          Return File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a))
                                      End Function)

            Return ordered
        End Function

        ''' <summary>最新的一次实验记录目录（没有任何记录时返回 ``Nothing``）。</summary>
        Public Shared Function Latest(root As String) As String
            Dim dirs As String() = Enumerate(root)

            If dirs.Length = 0 Then Return Nothing

            Return dirs(0)
        End Function

        ''' <summary>读一条实验记录的摘要（用于列表显示；读取失败时返回目录名）。</summary>
        Public Shared Function Describe(dir As String) As String
            Dim summary As String = IO.Path.Combine(dir, "stimulation_summary.csv")

            If Not File.Exists(summary) Then Return IO.Path.GetFileName(dir)

            Dim sb As New StringBuilder(IO.Path.GetFileName(dir))
            Dim neuron As String = Nothing
            Dim radius As String = Nothing
            Dim response As String = Nothing
            Dim potential As String = Nothing

            For Each line As String In File.ReadLines(summary)
                Dim cells As String() = line.Split(","c)

                If cells.Length < 2 Then Continue For

                Select Case cells(0)
                    Case "neuron_index"
                        neuron = cells(1)
                        potential = potential
                    Case "radius_um"
                        radius = cells(1)
                    Case "response_neurons"
                        response = cells(1)
                    Case "analysis_matches"
                        potential = cells(1)
                End Select
            Next

            Call sb.Append($": neuron #{neuron}")

            If radius IsNot Nothing Then Call sb.Append($", r={radius} um")
            If response IsNot Nothing Then Call sb.Append($", {response} responders")

            If String.Equals(potential, "True", StringComparison.OrdinalIgnoreCase) Then
                Call sb.Append(" (含膜电位)")
            End If

            Return sb.ToString
        End Function

    End Class

End Namespace
