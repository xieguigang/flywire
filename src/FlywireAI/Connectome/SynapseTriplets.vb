Imports FlywireAI.FAFBv783
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports std = System.Math

Namespace Connectome

    ''' <summary>
    ''' 由连接组表格 (``connections_princeton.csv``) 构建出来的突触三元组 ``(pre, post, weight)``
    ''' 以及权重与极性统计。
    ''' </summary>
    ''' <remarks>
    ''' **权重约定**：``weight = polarity(nt_type) × syn_count``，其中 ``polarity`` 在 ``nt_type``
    ''' 为 ``GABA`` (抑制性) 的时候取 ``-1``，``GLUT`` / ``ACH`` 等 (以及空值) 取 ``+1``，
    ''' 从而形成 E/I 平衡网络。
    ''' 
    ''' **结构归一化**：<see cref="NormalizeStructural"/> 按照**突触后神经元**的输入强度绝对值之和
    ''' (``Σ|w|``) 做缩放，使得每个神经元的总输入幅度为 1；随后由全局增益
    ''' (<see cref="ApplyGain"/>) 把权重缩放到合适的放电水平。
    ''' 
    ''' 这里没有使用框架的 <c>SparseMatrix.Normalize</c>：其对 FanIn/FanOut 采用**带符号求和**，
    ''' 在兴奋/抑制混杂 (和接近 0 或者为负) 的列表上是不可靠的。
    ''' </remarks>
    Public Class SynapseTriplets

        ''' <summary>神经元总数 N。</summary>
        Public ReadOnly Property Units As Integer

        ''' <summary>突触前神经元索引数组。</summary>
        Public ReadOnly Property Pre As Integer()

        ''' <summary>突触后神经元索引数组。</summary>
        Public ReadOnly Property Post As Integer()

        ''' <summary>带符号的原始权重数组 (±syn_count)，尚未做结构归一化。</summary>
        Public ReadOnly Property Weight As Double()

        ''' <summary>每个突触后神经元的输入强度绝对值之和 (原始、未合并)。</summary>
        Public ReadOnly Property RawFanIn As Double()

        ''' <summary>csv 文件之中的数据行数 (per-neuropil 行)。</summary>
        Public ReadOnly Property CsvRows As Long

        ''' <summary>兴奋性突触行数 (GLUT / ACH / 其它非 GABA 类型)。</summary>
        Public ReadOnly Property ExcitatoryCount As Long

        ''' <summary>抑制性突触行数 (GABA)。</summary>
        Public ReadOnly Property InhibitoryCount As Long

        ''' <summary>nt_type 为空的行数 (按照兴奋性处理)。</summary>
        Public ReadOnly Property EmptyNtTypeCount As Long

        ''' <summary>csv 之中 syn_count 的总和。</summary>
        Public ReadOnly Property SynapseTotal As Double

        ''' <summary>
        ''' 因为 root_id 不在（已冻结的）索引里而被跳过的行数。
        ''' </summary>
        ''' <remarks>
        ''' 正常情况下为 0：可视化端用 ``names.csv`` 建好的索引覆盖了连接表的全部端点。
        ''' 一旦不为 0，说明两份表来自不同的数据版本，结果会失真，报告里要能看见。
        ''' </remarks>
        Public ReadOnly Property UnresolvedRows As Long

        Private Sub New(units As Integer,
                        preIndices As Integer(),
                        postIndices As Integer(),
                        weights As Double(),
                        fanIn As Double(),
                        csvRows As Long,
                        excitatory As Long,
                        inhibitory As Long,
                        emptyNtType As Long,
                        synapseTotal As Double,
                        unresolvedRows As Long)

            Me.Units = units
            Me.Pre = preIndices
            Me.Post = postIndices
            Me.Weight = weights
            Me.RawFanIn = fanIn
            Me.CsvRows = csvRows
            Me.ExcitatoryCount = excitatory
            Me.InhibitoryCount = inhibitory
            Me.EmptyNtTypeCount = emptyNtType
            Me.SynapseTotal = synapseTotal
            Me.UnresolvedRows = unresolvedRows
        End Sub

        ''' <summary>单个突触后神经元的最大输入强度绝对值之和。</summary>
        Public ReadOnly Property MaxRawFanIn As Double
            Get
                If RawFanIn.Length = 0 Then Return 0
                Return RawFanIn.Max
            End Get
        End Property

        ''' <summary>输入强度绝对值之和的非零均值。</summary>
        Public ReadOnly Property MeanRawFanIn As Double
            Get
                Dim nonZero As Double() = RawFanIn.Where(Function(x) x > 0).ToArray

                If nonZero.Length = 0 Then
                    Return 0
                Else
                    Return nonZero.Average
                End If
            End Get
        End Property

        ''' <summary>
        ''' 构建 CSR 稀疏矩阵 (重复的 ``(pre, post)`` 会由框架按权重累加合并，
        ''' 即同一个细胞对在不同 neuropil 上的突触被合并为一条边)。
        ''' </summary>
        Public Function ToSparseMatrix() As SparseMatrix
            Return SparseMatrix.FromTriplets(Pre, Post, Weight, Units, Units)
        End Function

        ''' <summary>
        ''' 按照突触后神经元的 ``Σ|w|`` 就地进行结构归一化 (每个神经元的总输入幅度 = 1)。
        ''' </summary>
        ''' <returns>每个突触后神经元的 ``Σ|w|`` (归一化之前的值)。</returns>
        Public Shared Function NormalizeStructural(matrix As SparseMatrix) As Double()
            Dim values As Double() = matrix.Values
            Dim columns As Integer() = matrix.ColumnIndices
            Dim absSum As Double() = New Double(matrix.Columns - 1) {}

            For k As Integer = 0 To values.Length - 1
                absSum(columns(k)) += std.Abs(values(k))
            Next

            For k As Integer = 0 To values.Length - 1
                Dim total As Double = absSum(columns(k))

                ' 孤立神经元 (没有任何输入) 的 total 为 0，保持原值即可
                If total > 0 Then
                    values(k) /= total
                End If
            Next

            Return absSum
        End Function

        ''' <summary>
        ''' 按照基础权重与全局增益就地重写 CSR 的取值数组。
        ''' 
        ''' (在 CPU 张量后端之下，外部就地修改 <c>SparseMatrix.Values</c> 会被下一次 SpMM 立即采用，
        ''' 因此不需要重建 CSR 就可以做增益标定)
        ''' </summary>
        Public Shared Sub ApplyGain(matrix As SparseMatrix, baseValues As Double(), gain As Double)
            Dim values As Double() = matrix.Values

            If values.Length <> baseValues.Length Then
                Throw New ArgumentException(
                    $"基础权重数量 ({baseValues.Length}) 与 CSR 取值数量 ({values.Length}) 不一致")
            End If

            For k As Integer = 0 To values.Length - 1
                values(k) = baseValues(k) * gain
            Next

            ' 权重是就地改写的：必须让设备端（GPU）缓存的 CSR 副本失效。
            ' 漏掉这一步的后果不是"精度差一点"，而是 GPU 会继续用旧增益算整段仿真 ——
            ' 表现为「CPU 与 GPU 结果不同，且差异无法从任何参数上解释」，极难定位。
            Call matrix.MarkModified()
        End Sub

        ''' <summary>
        ''' 流式扫描连接表 (``connections_princeton.csv``) 并且构建突触三元组。
        ''' </summary>
        ''' <param name="index">
        ''' 连接组索引表；扫描过程中出现的未知 root_id 会被追加到索引之中，因此调用方需要在
        ''' 本函数返回之后再调用 <see cref="ConnectomeIndex.Freeze"/>。
        ''' </param>
        ''' <param name="csvPath">``connections_princeton.csv`` 的文件路径。</param>
        ''' <param name="excitatoryGain">兴奋性突触的极性增益 (调整 E/I 比例)。</param>
        ''' <param name="inhibitoryGain">抑制性突触的极性增益 (调整 E/I 比例)。</param>
        ''' <param name="progress">进度回调 (每 100 万行调用一次，参数为已处理的行数)。</param>
        ''' <remarks>
        ''' 只用于"还没有 msgpack 转储包"的场景（例如 ``--dump`` 本身）。
        ''' 应用侧应当走 <see cref="Build(ConnectomeIndex, Long(), Long(), Double(), Integer(), String(), Double, Double, Action(Of Long))"/>
        ''' 或 <see cref="BuildFromIndices"/>，直接用已经解析好的连接列。
        ''' </remarks>
        Public Shared Function Build(index As ConnectomeIndex,
                                     csvPath As String,
                                     Optional excitatoryGain As Double = 1.0,
                                     Optional inhibitoryGain As Double = 1.0,
                                     Optional progress As Action(Of Long) = Nothing) As SynapseTriplets

            If index Is Nothing Then
                Throw New ArgumentNullException(NameOf(index))
            End If
            If String.IsNullOrWhiteSpace(csvPath) Then
                Throw New ArgumentNullException(NameOf(csvPath))
            End If

            Return build(index, iterateCsv(csvPath), excitatoryGain, inhibitoryGain, progress)
        End Function

        ''' <summary>
        ''' 用<b>已经解析好的连接列</b>构建突触三元组（数据源是 msgpack 转储包，不再接触 csv）。
        ''' </summary>
        ''' <param name="preRoots">突触前 root_id。</param>
        ''' <param name="postRoots">突触后 root_id。</param>
        ''' <param name="synCount">突触数 (原始值)。</param>
        ''' <param name="ntCodes">递质类型的字典编号（下标指向 <paramref name="ntNames"/>）；没有就当空值处理。</param>
        ''' <param name="ntNames">递质类型名称表。</param>
        Public Shared Function Build(index As ConnectomeIndex,
                                     preRoots As Long(),
                                     postRoots As Long(),
                                     synCount As Double(),
                                     Optional ntCodes As Integer() = Nothing,
                                     Optional ntNames As String() = Nothing,
                                     Optional excitatoryGain As Double = 1.0,
                                     Optional inhibitoryGain As Double = 1.0,
                                     Optional progress As Action(Of Long) = Nothing) As SynapseTriplets

            If index Is Nothing Then
                Throw New ArgumentNullException(NameOf(index))
            End If
            If preRoots Is Nothing OrElse postRoots Is Nothing OrElse synCount Is Nothing Then
                Throw New ArgumentNullException("连接列不能为空")
            End If

            Return build(index, iterateColumns(preRoots, postRoots, synCount, ntCodes, ntNames),
                         excitatoryGain, inhibitoryGain, progress)
        End Function

        ''' <summary>
        ''' 用<b>已经解析成神经元下标</b>的连接构建突触三元组（连 root_id 查表都省掉）。
        ''' </summary>
        ''' <remarks>
        ''' 三维可视化端加载完数据集之后，<c>Pre / Post / SynCount / ConnectionNtType</c>
        ''' 已经是下标形式并已经按索引过滤过了，电刺激仿真直接复用它们即可 ——
        ''' 既不必再读一遍连接表，也不会出现"两端点不在索引里"的行。
        ''' </remarks>
        Public Shared Function BuildFromIndices(index As ConnectomeIndex,
                                                pre As Integer(),
                                                post As Integer(),
                                                synCount As Double(),
                                                Optional ntCodes As Integer() = Nothing,
                                                Optional ntNames As String() = Nothing,
                                                Optional excitatoryGain As Double = 1.0,
                                                Optional inhibitoryGain As Double = 1.0,
                                                Optional progress As Action(Of Long) = Nothing) As SynapseTriplets

            If index Is Nothing Then
                Throw New ArgumentNullException(NameOf(index))
            End If
            If pre Is Nothing OrElse post Is Nothing OrElse synCount Is Nothing Then
                Throw New ArgumentNullException("连接列不能为空")
            End If

            Return build(index, iterateIndexed(index, pre, post, synCount, ntCodes, ntNames),
                         excitatoryGain, inhibitoryGain, progress)
        End Function

        ''' <summary>csv 行 -> 统一的行形状。</summary>
        Private Shared Iterator Function iterateCsv(csvPath As String) As IEnumerable(Of SynapseRow)
            For Each row As Connections In csvPath.StreamConnections()
                Yield New SynapseRow With {
                    .PreRoot = row.PreRootId,
                    .PostRoot = row.PostRootId,
                    .SynCount = row.SynCount,
                    .NtType = If(row.NtType, "")
                }
            Next
        End Function

        ''' <summary>root_id 列 -> 统一的行形状。</summary>
        Private Shared Iterator Function iterateColumns(preRoots As Long(), postRoots As Long(),
                                                        synCount As Double(),
                                                        ntCodes As Integer(), ntNames As String()) As IEnumerable(Of SynapseRow)
            Dim count As Integer = System.Math.Min(preRoots.Length, System.Math.Min(postRoots.Length, synCount.Length))

            For i As Integer = 0 To count - 1
                Yield New SynapseRow With {
                    .PreRoot = preRoots(i),
                    .PostRoot = postRoots(i),
                    .SynCount = synCount(i),
                    .NtType = neurotransmitterOf(ntCodes, ntNames, i)
                }
            Next
        End Function

        ''' <summary>下标列 -> 统一的行形状（顺带把下标换回 root_id，交给核心统一解析）。</summary>
        Private Shared Iterator Function iterateIndexed(index As ConnectomeIndex,
                                                        pre As Integer(), post As Integer(),
                                                        synCount As Double(),
                                                        ntCodes As Integer(), ntNames As String()) As IEnumerable(Of SynapseRow)
            Dim count As Integer = System.Math.Min(pre.Length, System.Math.Min(post.Length, synCount.Length))

            For i As Integer = 0 To count - 1
                Yield New SynapseRow With {
                    .PreRoot = index.GetRootId(pre(i)),
                    .PostRoot = index.GetRootId(post(i)),
                    .SynCount = synCount(i),
                    .NtType = neurotransmitterOf(ntCodes, ntNames, i)
                }
            Next
        End Function

        Private Shared Function neurotransmitterOf(ntCodes As Integer(), ntNames As String(), i As Integer) As String
            If ntCodes Is Nothing OrElse ntNames Is Nothing Then Return ""
            If i < 0 OrElse i >= ntCodes.Length Then Return ""

            Dim code As Integer = ntCodes(i)

            If code < 0 OrElse code >= ntNames.Length Then Return ""

            Return If(ntNames(code), "")
        End Function

        ''' <summary>连接行的统一形状（结构而不是类：534 万行不能每行一个对象）。</summary>
        Private Structure SynapseRow
            Public PreRoot As Long
            Public PostRoot As Long
            Public SynCount As Double
            Public NtType As String
        End Structure

        ''' <summary>三种数据源共用的构建核心。</summary>
        Private Shared Function build(index As ConnectomeIndex,
                                      rows As IEnumerable(Of SynapseRow),
                                      excitatoryGain As Double,
                                      inhibitoryGain As Double,
                                      progress As Action(Of Long)) As SynapseTriplets

            Const progressEvery As Long = 1000000

            Dim preIndices As New List(Of Integer)()
            Dim postIndices As New List(Of Integer)()
            Dim weights As New List(Of Double)()
            Dim csvRows As Long = 0
            Dim excitatory As Long = 0
            Dim inhibitory As Long = 0
            Dim emptyNtType As Long = 0
            Dim synapseTotal As Double = 0

            Dim unresolved As Long = 0
            Dim frozen As Boolean = index.Frozen

            For Each row As SynapseRow In rows
                csvRows += 1L

                Dim preIndex As Integer
                Dim postIndex As Integer

                ' 索引已经冻结时不能再追加神经元。这是"可视化端先建好索引、
                ' 再交给仿真端"的用法：此时连接表里若出现索引之外的 root_id，只能跳过并计数
                ' （本数据集里连接表的端点全部落在主索引内，所以正常情况下不会命中）。
                If frozen Then
                    If Not index.IndexOf(row.PreRoot, preIndex) OrElse
                       Not index.IndexOf(row.PostRoot, postIndex) Then
                        unresolved += 1L

                        Continue For
                    End If
                Else
                    preIndex = index.GetOrAddIndex(row.PreRoot)
                    postIndex = index.GetOrAddIndex(row.PostRoot)
                End If

                Dim ntType As String = If(row.NtType, "").Trim
                Dim polarity As Double

                If ntType.Length = 0 Then
                    emptyNtType += 1L
                    polarity = 1.0
                ElseIf ConnectomeIndex.IsInhibitoryNeurotransmitter(ntType) Then
                    inhibitory += 1L
                    polarity = -1.0
                Else
                    excitatory += 1L
                    polarity = 1.0
                End If

                Dim polarityGain As Double = If(polarity < 0, inhibitoryGain, excitatoryGain)

                Call preIndices.Add(preIndex)
                Call postIndices.Add(postIndex)
                Call weights.Add(polarity * polarityGain * row.SynCount)

                synapseTotal += row.SynCount

                If Not progress Is Nothing AndAlso (csvRows Mod progressEvery) = 0 Then
                    Call progress(csvRows)
                End If
            Next

            Dim units As Integer = index.Size
            Dim rawFanIn As Double() = New Double(units - 1) {}

            For k As Integer = 0 To weights.Count - 1
                rawFanIn(postIndices(k)) += std.Abs(weights(k))
            Next

            Return New SynapseTriplets(
                units,
                preIndices.ToArray,
                postIndices.ToArray,
                weights.ToArray,
                rawFanIn,
                csvRows,
                excitatory,
                inhibitory,
                emptyNtType,
                synapseTotal,
                unresolved
            )
        End Function

        Public Overrides Function ToString() As String
            Dim text As String = $"{CsvRows} rows ({ExcitatoryCount} E / {InhibitoryCount} I / {EmptyNtTypeCount} no-nt), " &
                $"synapses={SynapseTotal}, neurons={Units}, max fan-in={MaxRawFanIn}"

            ' 只在真的发生了跳过时才提示：它是"索引与连接表不匹配"的信号
            If UnresolvedRows > 0 Then
                text &= $", {UnresolvedRows} rows skipped (unknown root_id)"
            End If

            Return text
        End Function

    End Class

    ''' <summary>
    ''' CSR 权重矩阵的统计信息 (用于报告与数值合法性检查)。
    ''' </summary>
    Public Class WeightStatistics

        Public Property NNZ As Integer
        Public Property Excitatory As Integer
        Public Property Inhibitory As Integer
        Public Property NaNCount As Integer
        Public Property InfinityCount As Integer
        Public Property Min As Double
        Public Property Max As Double
        Public Property MeanAbs As Double

        ''' <summary>是否存在非法的权重数值 (NaN / Inf)？</summary>
        Public ReadOnly Property IsValid As Boolean
            Get
                Return NaNCount = 0 AndAlso InfinityCount = 0
            End Get
        End Property

        Public Shared Function FromMatrix(matrix As SparseMatrix) As WeightStatistics
            Dim values As Double() = matrix.Values
            Dim stat As New WeightStatistics With {.NNZ = values.Length}

            If values.Length = 0 Then
                Return stat
            End If

            Dim min As Double = Double.MaxValue
            Dim max As Double = Double.MinValue
            Dim sum As Double = 0

            For Each w As Double In values
                If Double.IsNaN(w) Then
                    stat.NaNCount += 1
                    Continue For
                End If
                If Double.IsInfinity(w) Then
                    stat.InfinityCount += 1
                    Continue For
                End If

                If w < min Then min = w
                If w > max Then max = w

                sum += std.Abs(w)

                If w < 0 Then
                    stat.Inhibitory += 1
                Else
                    stat.Excitatory += 1
                End If
            Next

            If max < min Then
                stat.Min = 0
                stat.Max = 0
            Else
                stat.Min = min
                stat.Max = max
            End If

            stat.MeanAbs = sum / values.Length

            Return stat
        End Function

        Public Overrides Function ToString() As String
            Return $"nnz={NNZ} (E={Excitatory}, I={Inhibitory}), min={Min}, max={Max}, mean|w|={MeanAbs}, NaN={NaNCount}, Inf={InfinityCount}"
        End Function

    End Class
End Namespace
