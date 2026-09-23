Imports System.Globalization
Imports System.IO
Imports FlywireAI.Connectome
Imports Neuropils.Data
Imports Neuropils.Simulation

Namespace Analysis

    ''' <summary>筛选神经元集合所用的标签维度。</summary>
    Public Enum NeuronLabelDimension
        ''' <summary>主导脑区（neuropil）</summary>
        Neuropil
        ''' <summary>神经递质类型（nt_type）</summary>
        Neurotransmitter
        ''' <summary>细胞类型（primary_type）</summary>
        PrimaryType
        ''' <summary>分类层级：super_class</summary>
        SuperClass
        ''' <summary>分类层级：class</summary>
        CellClass
        ''' <summary>分类层级：group（与细胞类型同一层级的粗分类）</summary>
        CellGroup
    End Enum

    ''' <summary>纵轴"响应电信号强度"的取值口径。</summary>
    Public Enum ResponseSignalMode
        ''' <summary>触发前膜电位（来自分析回放，最忠实于神经元真实电信号）</summary>
        MembranePotential
        ''' <summary>滑动窗放电率（由记录下来的脉冲序列折算，任何一次实验都可用）</summary>
        SpikeRate
        ''' <summary>原始脉冲（0/1）</summary>
        Spike
    End Enum

    ''' <summary>
    ''' 电刺激实验的响应曲线数据源：把一次刺激的结果规整成
    ''' 「响应神经元 × 时间步」的响应矩阵，并提供按标签筛选与折算曲线的能力。
    ''' </summary>
    ''' <remarks>
    ''' 两种构造方式：
    ''' <list type="bullet">
    '''   <item><see cref="FromReplay"/>：直接用本次会话刚跑完的结果（含膜电位）；</item>
    '''   <item><see cref="FromReport"/>：从落盘的实验记录目录读回来 ——
    '''         于是"记录下来的响应结果数据"在重启程序之后仍然可以画图。</item>
    ''' </list>
    ''' 
    ''' 标签一律从 <see cref="BrainDataset"/> 与 <see cref="ConnectomeIndex"/> 现取，
    ''' 因此筛选维度与三维视图、图例用的是同一套注释信息。
    ''' </remarks>
    Public Class ResponseDataset

        ''' <summary>本次刺激的元信息（用于标题与状态栏）。</summary>
        Public Property Summary As String = ""

        ''' <summary>数据来源描述（会话内结果 / 记录目录）。</summary>
        Public Property Source As String = ""

        ''' <summary>时间步数 T。</summary>
        Public Property Steps As Integer

        ''' <summary>神经元总数 N。</summary>
        Public Property Units As Integer

        ''' <summary>被刺激的神经元索引（-1 表示未知）。</summary>
        Public Property TargetNeuron As Integer = -1

        ''' <summary>被刺激神经元的 root_id。</summary>
        Public Property TargetRootId As Long

        ''' <summary>募集半径 (微米)。</summary>
        Public Property RadiusUm As Double

        ''' <summary>注入电流强度。</summary>
        Public Property Current As Double

        ''' <summary>响应神经元索引（矩阵的行）。</summary>
        Public Property Responders As Integer() = New Integer() {}

        ''' <summary>膜电位矩阵（可能为空 —— 记录里没有分析回放时）</summary>
        Public Property Potential As Double()() = New Double()() {}

        ''' <summary>脉冲矩阵（0/1，逐神经元逐步；永远可用）</summary>
        Public Property Pulses As Double()() = New Double()() {}

        ''' <summary>逐神经元累计脉冲计数（长度 = <see cref="Units"/>；可能为空）。</summary>
        Public Property Counts As Double() = New Double() {}

        ''' <summary>分析回放与快速仿真的脉冲是否一致（``Nothing`` = 没有膜电位数据）。</summary>
        Public Property PotentialValidated As Boolean? = Nothing

        ''' <summary>不一致的神经元-步对数。</summary>
        Public Property PotentialMismatches As Integer

        ''' <summary>动作电位阈值（用于在图上标出阈值线）。</summary>
        Public Property Threshold As Double = 1.0

        ''' <summary>是否拿到了膜电位轨迹。</summary>
        Public ReadOnly Property HasPotential As Boolean
            Get
                Return Potential IsNot Nothing AndAlso Potential.Length = Responders.Length AndAlso Potential.Length > 0
            End Get
        End Property

        ''' <summary>当前数据可用的信号口径。</summary>
        Public ReadOnly Property AvailableModes As ResponseSignalMode()
            Get
                If HasPotential Then
                    Return {ResponseSignalMode.MembranePotential, ResponseSignalMode.SpikeRate, ResponseSignalMode.Spike}
                End If

                Return {ResponseSignalMode.SpikeRate, ResponseSignalMode.Spike}
            End Get
        End Property

        ''' <summary>响应神经元的累计脉冲数（用于"最活跃"排序）。</summary>
        Public Function TotalSpikes(neuron As Integer) As Double
            If Counts IsNot Nothing AndAlso neuron >= 0 AndAlso neuron < Counts.Length Then
                Return Counts(neuron)
            End If

            Return 0
        End Function

#Region "构造"

        ''' <summary>用本次会话刚跑完的刺激结果构造。</summary>
        Public Shared Function FromReplay(replay As StimulationReplay,
                                          dataset As BrainDataset,
                                          Optional threshold As Double = 1.0) As ResponseDataset

            If replay Is Nothing Then Throw New ArgumentNullException(NameOf(replay))

            Dim responders As Integer() = If(replay.ResponseNeurons IsNot Nothing AndAlso replay.ResponseNeurons.Length > 0,
                                             replay.ResponseNeurons,
                                             unionOf(replay))
            Dim pulses As Double()() = pulsesOf(replay, responders)

            Return New ResponseDataset With {
                .Source = "本次会话的刺激结果",
                .Steps = replay.Steps,
                .Units = replay.Units,
                .TargetNeuron = replay.Neuron,
                .TargetRootId = replay.RootId,
                .RadiusUm = replay.RadiusNm / 1000.0,
                .Current = replay.Strength,
                .Counts = replay.Counts,
                .Threshold = threshold,
                .Summary = replay.Describe(),
                .Responders = responders,
                .Pulses = pulses,
                .Potential = If(replay.ResponsePotential, New Double()() {}),
                .PotentialValidated = If(replay.AnalysisMatches.HasValue, replay.AnalysisMatches.Value, CType(Nothing, Boolean?)),
                .PotentialMismatches = replay.AnalysisMismatches
            }
        End Function

        ''' <summary>
        ''' 从落盘的实验记录目录构造（读取 summary / active_neurons / response_potential / activity 四个文件）。
        ''' </summary>
        Public Shared Function FromReport(dir As String, dataset As BrainDataset) As ResponseDataset
            If Not Directory.Exists(dir) Then
                Throw New DirectoryNotFoundException($"刺激记录目录不存在: {dir}")
            End If

            ' 一律写 IO.Path.*：本文件里有若干名为 path 的参数，VB 不区分大小写，
            ' 直接用 Path 会被遮蔽成那个字符串参数
            Dim summary As Dictionary(Of String, String) = readSummary(IO.Path.Combine(dir, "stimulation_summary.csv"))
            Dim result As New ResponseDataset With {
                .Source = dir,
                .Units = If(dataset Is Nothing, 0, dataset.Units)
            }

            Call result.applySummary(summary)

            Dim active = readActiveNeurons(IO.Path.Combine(dir, "stimulation_active_neurons.csv"))

            If active.Neurons Is Nothing OrElse active.Neurons.Length = 0 Then
                Throw New InvalidDataException($"记录目录缺少（或为空的）stimulation_active_neurons.csv: {dir}")
            End If

            If result.Steps <= 0 Then result.Steps = active.Steps
            If result.Units <= 0 Then result.Units = dataset?.Units

            ' 脉冲矩阵：文件里的行序就是"首次出现顺序"，这里按神经元编号重排，便于与膜电位矩阵对齐
            result.Responders = sortedCopy(active.Neurons)
            result.Pulses = realignPulses(active, result.Responders, result.Steps)
            result.Counts = readActivity(IO.Path.Combine(dir, "neuron_activity_stimulation.csv"), result.Units)

            Dim potentialFile As String = IO.Path.Combine(dir, "stimulation_response_potential.csv")

            If File.Exists(potentialFile) Then
                Dim loaded = readPotential(potentialFile, result.Steps)

                result.Responders = loaded.Neurons
                result.Potential = loaded.Matrix
                result.Pulses = realignTo(active, loaded.Neurons, result.Steps, loaded.Steps)
                result.PotentialValidated = True
                result.PotentialMismatches = 0

                If loaded.Steps > result.Steps Then result.Steps = loaded.Steps
            End If

            Return result
        End Function

        Private Shared Function unionOf(replay As StimulationReplay) As Integer()
            Dim members As New List(Of Integer)(4096)

            If replay.Counts IsNot Nothing Then
                For i As Integer = 0 To replay.Counts.Length - 1
                    If replay.Counts(i) > 0 Then Call members.Add(i)
                Next
            End If

            Return members.ToArray()
        End Function

        Private Shared Function pulsesOf(replay As StimulationReplay, responders As Integer()) As Double()()
            Dim rows As Double()() = New Double(responders.Length - 1)() {}

            For k As Integer = 0 To responders.Length - 1
                rows(k) = New Double(System.Math.Max(0, replay.Steps - 1)) {}
            Next

            For t As Integer = 0 To replay.Steps - 1
                Dim active As Integer() = replay.StepActive(t)

                For i As Integer = 0 To active.Length - 1
                    Dim row As Integer = System.Array.IndexOf(responders, active(i))

                    If row >= 0 AndAlso t < rows(row).Length Then
                        rows(row)(t) = 1.0
                    End If
                Next
            Next

            Return rows
        End Function

#End Region

#Region "标签"

        ''' <summary>没有注释信息时的标签名。</summary>
        Public Const Unannotated As String = "(未注释)"

        ''' <summary>取一个神经元的标签值（该维度上没有注释时返回 <see cref="Unannotated"/>）。</summary>
        Public Function LabelOf(dimension As NeuronLabelDimension, neuron As Integer, dataset As BrainDataset) As String
            Dim value As String = Nothing

            If dataset IsNot Nothing Then
                Select Case dimension
                    Case NeuronLabelDimension.Neuropil
                        value = dataset.GetNeuropilName(neuron)
                    Case NeuronLabelDimension.Neurotransmitter
                        If dataset.Neurotransmitters IsNot Nothing AndAlso neuron < dataset.Neurotransmitters.Length Then
                            value = dataset.Neurotransmitters(neuron)
                        End If
                    Case NeuronLabelDimension.PrimaryType
                        value = dataset.Index.GetPrimaryType(neuron)
                    Case NeuronLabelDimension.SuperClass
                        value = dataset.Index.GetSuperClass(neuron)
                    Case NeuronLabelDimension.CellClass
                        value = dataset.Index.GetClass(neuron)
                    Case NeuronLabelDimension.CellGroup
                        value = dataset.Index.GetGroup(neuron)
                End Select
            End If

            If String.IsNullOrWhiteSpace(value) Then Return Unannotated

            Return value.Trim
        End Function

        ''' <summary>
        ''' 统计某个标签维度在<b>响应神经元集合</b>里出现的取值与数量。
        ''' </summary>
        ''' <remarks>
        ''' 只统计有响应的神经元：注释表里上百个类别中绝大多数在当前刺激下根本没有神经元
        ''' 被激活，列出来只会让筛选面板变成噪音。
        ''' </remarks>
        Public Function Categories(dimension As NeuronLabelDimension,
                                   dataset As BrainDataset,
                                   ByRef counts As Integer()) As String()

            Dim tally As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
            Dim ordered As New List(Of String)()

            For k As Integer = 0 To Responders.Length - 1
                Dim name As String = LabelOf(dimension, Responders(k), dataset)
                Dim n As Integer

                If tally.TryGetValue(name, n) Then
                    tally(name) = n + 1
                Else
                    tally(name) = 1
                    Call ordered.Add(name)
                End If
            Next

            Call ordered.Sort(StringComparer.Ordinal)

            Dim values As String() = ordered.ToArray()
            Dim counter As Integer() = New Integer(values.Length - 1) {}

            For i As Integer = 0 To values.Length - 1
                counter(i) = tally(values(i))
            Next

            counts = counter

            Return values
        End Function

        ''' <summary>选中标签集合内的响应神经元行号（未选中任何值时返回全部）。</summary>
        Public Function SelectRows(dimension As NeuronLabelDimension,
                                   dataset As BrainDataset,
                                   selected As IEnumerable(Of String)) As Integer()

            Dim filter As HashSet(Of String) = Nothing

            If selected IsNot Nothing Then
                filter = New HashSet(Of String)(selected, StringComparer.Ordinal)

                If filter.Count = 0 Then filter = Nothing
            End If

            Dim rows As New List(Of Integer)(Responders.Length)

            For k As Integer = 0 To Responders.Length - 1
                If filter Is Nothing OrElse filter.Contains(LabelOf(dimension, Responders(k), dataset)) Then
                    Call rows.Add(k)
                End If
            Next

            Return rows.ToArray()
        End Function

        ''' <summary>按累计脉冲数取最活跃的若干行（用于"曲线数量上限"的取舍）。</summary>
        Public Function TopRows(rows As Integer(), limit As Integer) As Integer()
            If limit <= 0 OrElse rows.Length <= limit Then Return rows

            Dim ranked As Integer() = CType(rows.Clone(), Integer())

            System.Array.Sort(ranked, Function(a As Integer, b As Integer)
                                          Return TotalSpikes(Responders(b)).CompareTo(TotalSpikes(Responders(a)))
                                      End Function)

            Dim head As Integer() = New Integer(limit - 1) {}

            System.Array.Copy(ranked, head, limit)

            Return head
        End Function

#End Region

#Region "曲线"

        ''' <summary>
        ''' 取第 <paramref name="row"/> 行的响应曲线（按所选口径折算）。
        ''' </summary>
        ''' <param name="row">响应矩阵行号</param>
        ''' <param name="mode">信号口径</param>
        ''' <param name="window">放电率的滑动窗宽（步），仅在 <see cref="ResponseSignalMode.SpikeRate"/> 下使用</param>
        Public Function Curve(row As Integer, mode As ResponseSignalMode, Optional window As Integer = 5) As Double()
            If row < 0 OrElse row >= Responders.Length Then Return New Double() {}

            Select Case mode
                Case ResponseSignalMode.MembranePotential
                    If HasPotential Then Return Potential(row)

                    ' 没有膜电位数据时静默回退到放电率：曲线仍然可用，只是口径不同（界面会标注）
                    Return If(row < Pulses.Length, smoothed(Pulses(row), window), New Double() {})
                Case ResponseSignalMode.SpikeRate
                    Return If(row < Pulses.Length, smoothed(Pulses(row), window), New Double() {})
                Case Else
                    Return If(row < Pulses.Length, Pulses(row), New Double() {})
            End Select
        End Function

        ''' <summary>
        ''' 脉冲 → 滑动窗放电率。
        ''' </summary>
        ''' <remarks>
        ''' 用居中滑动窗（窗口在两侧各取 <c>window/2</c> 步）而不是累积窗：
        ''' 累积窗会把峰值整体右移，看上去像是"响应比实际发生得更晚"。
        ''' 归一化成"每步发放概率"（窗内脉冲数 / 窗内步数），与膜电位同量纲、可比。
        ''' </remarks>
        Private Shared Function smoothed(pulses As Double(), window As Integer) As Double()
            If pulses Is Nothing Then Return New Double() {}

            If window <= 1 Then Return CType(pulses.Clone(), Double())

            Dim half As Integer = window \ 2
            Dim result As Double() = New Double(pulses.Length - 1) {}

            For t As Integer = 0 To pulses.Length - 1
                Dim from As Integer = System.Math.Max(0, t - half)
                Dim last As Integer = System.Math.Min(pulses.Length - 1, t + half)
                Dim total As Double = 0

                For i As Integer = from To last
                    total += pulses(i)
                Next

                result(t) = total / ((last - from) + 1)
            Next

            Return result
        End Function

#End Region

#Region "读取落盘记录"

        Private Shared Function readSummary(path As String) As Dictionary(Of String, String)
            Dim table As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

            If Not File.Exists(path) Then Return table

            For Each line As String In File.ReadLines(path)
                Dim cells As String() = line.Split(","c)

                If cells.Length >= 2 AndAlso Not String.Equals(cells(0), "key", StringComparison.OrdinalIgnoreCase) Then
                    table(cells(0)) = cells(1)
                End If
            Next

            Return table
        End Function

        ''' <remarks>
        ''' 形参不能叫 <c>summary</c>：VB 不区分大小写，它会遮蔽 <see cref="Summary"/> 属性，
        ''' 于是下面那句 "Summary = ..." 变成给字典赋值。
        ''' </remarks>
        Private Sub applySummary(table As Dictionary(Of String, String))
            Dim value As String = Nothing

            If table.TryGetValue("neuron_index", value) Then Integer.TryParse(value, TargetNeuron)
            If table.TryGetValue("root_id", value) Then Long.TryParse(value, TargetRootId)
            If table.TryGetValue("time_steps", value) Then Integer.TryParse(value, Steps)
            If table.TryGetValue("radius_um", value) Then Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, RadiusUm)
            If table.TryGetValue("strength", value) Then Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, Current)

            If table.TryGetValue("neurons", value) Then
                Dim units As Integer

                If Integer.TryParse(value, units) AndAlso units > 0 Then Units = units
            End If

            Summary = $"记录目录 {IO.Path.GetFileName(If(Source, ""))}：神经元 #{TargetNeuron} (root_id {TargetRootId}), " &
                      $"半径 {RadiusUm:F0} μm, 电流 {Current:F2}, T={Steps}"
        End Sub

        ''' <summary>
        ''' 读取"逐步激活神经元清单"（<c>step,neuron_index,root_id</c>）。
        ''' </summary>
        ''' <returns>脉冲矩阵（行 = <see cref="Neurons"/> 的顺序）与神经元清单、步数。</returns>
        Private Shared Function readActiveNeurons(path As String) As (Pulses As Double()(), Neurons As Integer(), Steps As Integer)
            If Not File.Exists(path) Then Return (Nothing, Nothing, 0)

            Dim rows As New Dictionary(Of Integer, Integer)(65536)
            Dim members As New List(Of Integer)(8192)
            ' 元组元素名不能叫 Step（VB 保留字）
            Dim events As New List(Of (Row As Integer, StepIndex As Integer))(262144)
            Dim stepCount As Integer = 0

            For Each line As String In File.ReadLines(path)
                Dim cells As String() = line.Split(","c)
                Dim neuron As Integer
                Dim t As Integer

                If cells.Length < 2 Then Continue For
                If Not Integer.TryParse(cells(1), neuron) Then Continue For
                If Not Integer.TryParse(cells(0), t) Then Continue For

                If t > stepCount Then stepCount = t

                Dim row As Integer

                If Not rows.TryGetValue(neuron, row) Then
                    row = members.Count
                    rows(neuron) = row
                    Call members.Add(neuron)
                End If

                ' 同一步里同一个神经元只应出现一次；重复出现时用"或"合并
                Call events.Add((row, t))
            Next

            Dim matrix As Double()() = New Double(members.Count - 1)() {}

            For k As Integer = 0 To members.Count - 1
                matrix(k) = New Double(System.Math.Max(0, stepCount - 1)) {}
            Next

            For i As Integer = 0 To events.Count - 1
                Dim e = events(i)

                If e.StepIndex >= 1 AndAlso e.StepIndex <= stepCount Then
                    matrix(e.Row)(e.StepIndex - 1) = 1.0
                End If
            Next

            Return (matrix, members.ToArray(), stepCount)
        End Function

        ''' <summary>读取膜电位矩阵（<c>neuron_index,root_id,t1,t2,...</c>）。</summary>
        Private Shared Function readPotential(path As String,
                                              steps As Integer) As (Matrix As Double()(), Neurons As Integer(), Steps As Integer)

            Dim table As New Dictionary(Of Integer, Double())(65536)
            Dim members As New List(Of Integer)(8192)
            Dim width As Integer = steps

            For Each line As String In File.ReadLines(path)
                Dim cells As String() = line.Split(","c)
                Dim neuron As Integer

                If cells.Length < 3 Then Continue For
                If Not Integer.TryParse(cells(0), neuron) Then Continue For

                Dim values As Double() = New Double(cells.Length - 3) {}

                For i As Integer = 2 To cells.Length - 1
                    Dim parsed As Double = 0

                    Double.TryParse(cells(i), NumberStyles.Float, CultureInfo.InvariantCulture, parsed)
                    values(i - 2) = parsed
                Next

                table(neuron) = values
                Call members.Add(neuron)

                If values.Length > width Then width = values.Length
            Next

            Dim neurons As Integer() = members.ToArray()
            Dim matrix As Double()() = New Double(neurons.Length - 1)() {}

            For k As Integer = 0 To neurons.Length - 1
                Dim values As Double() = table(neurons(k))
                Dim row As Double() = New Double(width - 1) {}

                For i As Integer = 0 To values.Length - 1
                    row(i) = values(i)
                Next

                matrix(k) = row
            Next

            Return (matrix, neurons, width)
        End Function

        ''' <summary>读取逐神经元累计脉冲计数（<c>neuron_index,root_id,spike_count,firing_rate</c>）。</summary>
        Private Shared Function readActivity(path As String, units As Integer) As Double()
            If units <= 0 OrElse Not File.Exists(path) Then Return New Double() {}

            Dim counts As Double() = New Double(units - 1) {}

            For Each line As String In File.ReadLines(path)
                Dim cells As String() = line.Split(","c)
                Dim index As Integer
                Dim value As Double

                If cells.Length < 3 Then Continue For
                If Not Integer.TryParse(cells(0), index) Then Continue For
                If Not Double.TryParse(cells(2), NumberStyles.Float, CultureInfo.InvariantCulture, value) Then Continue For
                If index >= 0 AndAlso index < units Then counts(index) = value
            Next

            Return counts
        End Function

        ''' <summary>把脉冲矩阵重排到指定神经元顺序（缺行的补零）。</summary>
        Private Shared Function realignTo(active As (Pulses As Double()(), Neurons As Integer(), Steps As Integer),
                                          neurons As Integer(),
                                          steps As Integer,
                                          fallbackSteps As Integer) As Double()()
            Dim rows As New Dictionary(Of Integer, Integer)(65536)

            If active.Neurons IsNot Nothing Then
                For k As Integer = 0 To active.Neurons.Length - 1
                    rows(active.Neurons(k)) = k
                Next
            End If

            Dim width As Integer = System.Math.Max(System.Math.Max(steps, active.Steps), fallbackSteps)
            Dim matrix As Double()() = New Double(neurons.Length - 1)() {}

            For k As Integer = 0 To neurons.Length - 1
                Dim row As Double() = New Double(System.Math.Max(0, width - 1)) {}
                Dim index As Integer

                If rows.TryGetValue(neurons(k), index) AndAlso index < active.Pulses.Length Then
                    Dim source As Double() = active.Pulses(index)

                    For t As Integer = 0 To source.Length - 1
                        If t < row.Length Then row(t) = source(t)
                    Next
                End If

                matrix(k) = row
            Next

            Return matrix
        End Function

        Private Shared Function sortedCopy(values As Integer()) As Integer()
            Dim copy As Integer() = CType(values.Clone(), Integer())

            System.Array.Sort(copy)

            Return copy
        End Function

        ''' <summary>按结果顺序重建脉冲矩阵（<see cref="FromReport"/> 的第一版，行序保持文件顺序）。</summary>
        Private Shared Function realignPulses(active As (Pulses As Double()(), Neurons As Integer(), Steps As Integer),
                                              neurons As Integer(),
                                              steps As Integer) As Double()()
            Dim order As New Dictionary(Of Integer, Integer)(65536)

            For k As Integer = 0 To active.Neurons.Length - 1
                order(active.Neurons(k)) = k
            Next

            Dim matrix As Double()() = New Double(neurons.Length - 1)() {}

            For k As Integer = 0 To neurons.Length - 1
                matrix(k) = active.Pulses(order(neurons(k)))
            Next

            Return matrix
        End Function

#End Region

    End Class

End Namespace
