Imports System.Linq
Imports std = System.Math
Imports FlywireAI.FAFBv783
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork

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

        Private Sub New(units As Integer,
                        preIndices As Integer(),
                        postIndices As Integer(),
                        weights As Double(),
                        fanIn As Double(),
                        csvRows As Long,
                        excitatory As Long,
                        inhibitory As Long,
                        emptyNtType As Long,
                        synapseTotal As Double)

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
        End Sub

        ''' <summary>
        ''' 流式扫描连接表并且构建突触三元组。
        ''' </summary>
        ''' <param name="index">
        ''' 连接组索引表；扫描过程中出现的未知 root_id 会被追加到索引之中，因此调用方需要在
        ''' 本函数返回之后再调用 <see cref="ConnectomeIndex.Freeze"/>。
        ''' </param>
        ''' <param name="csvPath">``connections_princeton.csv`` 的文件路径。</param>
        ''' <param name="excitatoryGain">兴奋性突触的极性增益 (调整 E/I 比例)。</param>
        ''' <param name="inhibitoryGain">抑制性突触的极性增益 (调整 E/I 比例)。</param>
        ''' <param name="progress">进度回调 (每 100 万行调用一次，参数为已处理的行数)。</param>
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

            Const progressEvery As Long = 1000000

            Dim preIndices As New List(Of Integer)()
            Dim postIndices As New List(Of Integer)()
            Dim weights As New List(Of Double)()
            Dim csvRows As Long = 0
            Dim excitatory As Long = 0
            Dim inhibitory As Long = 0
            Dim emptyNtType As Long = 0
            Dim synapseTotal As Double = 0

            ' 流式读取：不把 534 万行 Connections 对象常驻内存
            For Each row As Connections In csvPath.StreamConnections()
                csvRows += 1L

                Dim preIndex As Integer = index.GetOrAddIndex(row.PreRootId)
                Dim postIndex As Integer = index.GetOrAddIndex(row.PostRootId)
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
                synapseTotal
            )
        End Function

        Public Overrides Function ToString() As String
            Return $"{CsvRows} rows ({ExcitatoryCount} E / {InhibitoryCount} I / {EmptyNtTypeCount} no-nt), " &
                $"synapses={SynapseTotal}, neurons={Units}, max fan-in={MaxRawFanIn}"
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
