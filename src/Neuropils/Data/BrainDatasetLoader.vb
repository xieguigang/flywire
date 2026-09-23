Imports System.Globalization
Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports FlywireAI.Connectome
Imports FlywireAI.FAFBv783
Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace Data

    ''' <summary>
    ''' 把 FAFB v783 的表格装配成 <see cref="BrainDataset"/>。
    ''' </summary>
    ''' <remarks>
    ''' 全部表格都以<b>流式</b>方式读取（``Stream*`` 扩展方法，惰性迭代、O(1) 内存），
    ''' 因为其中连接表有 261 MB / 534 万行、脑区表有 321 列。整个加载过程可以在后台线程
    ''' 上运行，并通过 <paramref name="progress"/> 与 <paramref name="cancel"/> 反馈进度
    ''' 与响应取消。
    ''' 
    ''' 数据来源与用途：
    ''' 
    ''' * ``names.csv`` → 神经元主索引 (<see cref="ConnectomeIndex"/>)；
    ''' * ``classification.csv`` / ``consolidated_cell_types.csv`` / ``neurons.csv`` → 注释；
    ''' * ``coordinates.csv`` → 每个神经元的三维坐标 (点云的位置)；
    ''' * ``neuropil_synapse_table.csv`` → 每个神经元的主导脑区 (颜色维度之一)；
    ''' * ``connections_princeton.csv`` → 突触连接 (三维连线)；
    ''' * ``neuron_activity_*.csv`` → 仿真活跃度 (可选，找不到时界面可以现场重跑仿真)。
    ''' </remarks>
    Public Class BrainDatasetLoader

        ''' <summary>连接表的默认读取上限 (534 万行 × 5 个并行数组约 100 MB)。</summary>
        Public Const DefaultMaxConnections As Integer = 6000000

        Private ReadOnly m_config As VisualizationConfig

        Public Sub New(config As VisualizationConfig)
            If config Is Nothing Then Throw New ArgumentNullException(NameOf(config))

            m_config = config
        End Sub

        ''' <summary>连接表的读取上限 (超过之后截断，并在进度里报告)。</summary>
        Public Property MaxConnections As Integer = DefaultMaxConnections

        ''' <summary>
        ''' 加载整个数据集。
        ''' </summary>
        ''' <param name="progress">进度回调 (已经格式化的文本)</param>
        ''' <param name="cancel">取消标记</param>
        ''' <exception cref="FileNotFoundException">必需的表格不存在</exception>
        ''' <exception cref="OperationCanceledException">调用方取消了加载</exception>
        Public Function Load(Optional progress As Action(Of String) = Nothing,
                             Optional cancel As CancellationToken = Nothing) As BrainDataset

            Call validateFiles()

            Dim dataset As New BrainDataset With {
                .Source = m_config.DataDir
            }

            ' 1) 神经元主索引 + 注释
            Call report(progress, $"loading neuron index from {m_config.NamesCsv} ...")

            Dim names As List(Of CellNames) = m_config.ResolvePath(m_config.NamesCsv).LoadCellNames()
            Dim index As New ConnectomeIndex()

            For Each cell As CellNames In names
                Call index.Add(cell.RootId)
            Next

            Call index.Freeze()

            Call report(progress, $"  {index.Size} neurons, attaching annotations ...")

            Call index.AttachAnnotations(names,
                                         m_config.ResolvePath(m_config.ClassificationCsv).LoadClassification(),
                                         m_config.ResolvePath(m_config.CellTypesCsv).LoadCellTypes(),
                                         m_config.ResolvePath(m_config.NeuronsCsv).LoadNeurons())

            dataset.Index = index
            Call report(progress, $"  {index}")

            ' 2) 递质类型 (ConnectomeIndex 只保留了"是否抑制性"，着色需要类型本身)
            Call throwIfCancelled(cancel)
            Call report(progress, "loading neurotransmitter types ...")

            dataset.Neurotransmitters = loadNeurotransmitters(index, cancel)

            ' 3) 三维坐标
            Call throwIfCancelled(cancel)
            Call report(progress, $"loading coordinates from {m_config.CoordinatesCsv} ...")

            Call loadPositions(dataset, progress, cancel)

            ' 4) 主导脑区
            Call throwIfCancelled(cancel)
            Call report(progress, $"loading neuropil assignments from {m_config.NeuropilTableCsv} ...")

            Call loadNeuropils(dataset, progress, cancel)

            ' 5) 连接表
            Call throwIfCancelled(cancel)
            Call report(progress, $"loading connections from {m_config.ConnectionsCsv} ...")

            Call loadConnections(dataset, progress, cancel)

            ' 6) 仿真活跃度 (可选)
            Call throwIfCancelled(cancel)
            Call loadActivity(dataset, progress)

            Call report(progress, $"dataset ready: {dataset}")

            Return dataset
        End Function

#Region "loading steps"

        ''' <summary>每神经元的递质类型 (``neurons.csv``)。</summary>
        Private Function loadNeurotransmitters(index As ConnectomeIndex, cancel As CancellationToken) As String()
            Dim types As String() = New String(index.Size - 1) {}
            Dim i As Integer

            For Each cell As Neurons In m_config.ResolvePath(m_config.NeuronsCsv).StreamNeurons()
                Call throwIfCancelled(cancel)

                If cell Is Nothing OrElse Not index.IndexOf(cell.RootId, i) Then
                    Continue For
                End If

                types(i) = If(cell.NtType, "")
            Next

            ' 空值统一成一个可显示的名字，避免界面上出现空白图例项
            For k As Integer = 0 To types.Length - 1
                If String.IsNullOrWhiteSpace(types(k)) Then
                    types(k) = "unknown"
                End If
            Next

            Return types
        End Function

        ''' <summary>
        ''' 每个神经元的三维坐标：``coordinates.csv`` 对同一个神经元可能有多条标记位置，
        ''' 这里取它们的算术平均 (并记录标记条数)。
        ''' </summary>
        Private Sub loadPositions(dataset As BrainDataset, progress As Action(Of String), cancel As CancellationToken)
            Dim units As Integer = dataset.Units
            Dim index As ConnectomeIndex = dataset.Index
            Dim sums As Double() = New Double(units * 3 - 1) {}
            Dim marks As Integer() = New Integer(units - 1) {}
            Dim i As Integer
            Dim rows As Long = 0

            For Each cell As Coordinates In m_config.ResolvePath(m_config.CoordinatesCsv).StreamCoordinates()
                Call throwIfCancelled(cancel)

                rows += 1

                If (rows Mod 50000) = 0 Then
                    Call report(progress, $"  {rows} coordinate rows ...")
                End If

                If cell Is Nothing OrElse Not index.IndexOf(cell.RootId, i) Then
                    Continue For
                End If

                Dim position As NeuronPosition

                If Not PositionParser.TryParse(cell.Position, position) Then
                    Continue For
                End If

                ' 全零坐标是占位值 (没有标记位置)，不计入平均
                If position.IsOrigin Then
                    Continue For
                End If

                sums(i * 3) += position.X
                sums(i * 3 + 1) += position.Y
                sums(i * 3 + 2) += position.Z
                marks(i) += 1
            Next

            Dim positions As Double() = New Double(units * 3 - 1) {}
            Dim positioned As Integer = 0

            For k As Integer = 0 To units - 1
                If marks(k) > 0 Then
                    positions(k * 3) = sums(k * 3) / marks(k)
                    positions(k * 3 + 1) = sums(k * 3 + 1) / marks(k)
                    positions(k * 3 + 2) = sums(k * 3 + 2) / marks(k)
                    positioned += 1
                Else
                    positions(k * 3) = Double.NaN
                    positions(k * 3 + 1) = Double.NaN
                    positions(k * 3 + 2) = Double.NaN
                End If
            Next

            dataset.Positions = positions
            dataset.PositionMarks = marks

            Call report(progress, $"  {rows} coordinate rows -> {positioned}/{units} neurons have a position")
        End Sub

        ''' <summary>
        ''' 每个神经元的主导脑区：在 321 列的脑区表里取"输入+输出突触数最多"的脑区。
        ''' </summary>
        ''' <remarks>
        ''' 列名通过 <see cref="ColumnAttribute"/> 反射得到（``input synapses in AL_L`` 之类），
        ''' 属性名本身被简写成 VB 标识符，只有列名保留着脑区前缀。取值走
        ''' <see cref="Delegate.CreateDelegate(Type, MethodInfo)"/> 绑定出来的委托而不是
        ''' ``PropertyInfo.GetValue``：134,181 行 × 160 列 = 2100 万次读取，反射调用会慢一个数量级。
        ''' </remarks>
        Private Sub loadNeuropils(dataset As BrainDataset, progress As Action(Of String), cancel As CancellationToken)
            Dim schema As NeuropilSchema = NeuropilSchema.Create()
            Dim units As Integer = dataset.Units
            Dim assignment As Integer() = New Integer(units - 1) {}
            Dim strength As Double() = New Double(units - 1) {}
            Dim totals As Double() = New Double(schema.Regions.Length - 1) {}
            Dim i As Integer
            Dim rows As Long = 0
            Dim assigned As Integer = 0

            For k As Integer = 0 To assignment.Length - 1
                assignment(k) = -1
            Next

            For Each row As NeuropilSynapseTable In m_config.ResolvePath(m_config.NeuropilTableCsv).StreamNeuropilSynapseTable()
                Call throwIfCancelled(cancel)

                rows += 1

                If (rows Mod 20000) = 0 Then
                    Call report(progress, $"  {rows} neuropil rows ...")
                End If

                If row Is Nothing OrElse Not dataset.Index.IndexOf(row.RootId, i) Then
                    Continue For
                End If

                Array.Clear(totals, 0, totals.Length)

                For Each column As NeuropilColumn In schema.Columns
                    totals(column.Region) += column.Getter(row)
                Next

                Dim best As Integer = -1
                Dim bestValue As Double = 0

                For r As Integer = 0 To totals.Length - 1
                    If totals(r) > bestValue Then
                        bestValue = totals(r)
                        best = r
                    End If
                Next

                If best >= 0 Then
                    assignment(i) = best
                    strength(i) = bestValue
                    assigned += 1
                End If
            Next

            dataset.Neuropil = assignment
            dataset.NeuropilNames = schema.Regions
            dataset.NeuropilSynapses = strength

            Call report(progress, $"  {rows} rows -> {assigned}/{units} neurons assigned to {schema.Regions.Length} neuropils")
        End Sub

        ''' <summary>
        ''' 连接表：``pre_root_id, post_root_id, neuropil, syn_count, nt_type``。
        ''' </summary>
        Private Sub loadConnections(dataset As BrainDataset, progress As Action(Of String), cancel As CancellationToken)
            Dim index As ConnectomeIndex = dataset.Index
            Dim capacity As Integer = System.Math.Min(MaxConnections, 5400000)

            Dim pre As New List(Of Integer)(capacity)
            Dim post As New List(Of Integer)(capacity)
            Dim synapses As New List(Of Integer)(capacity)
            Dim neuropils As New List(Of Integer)(capacity)
            Dim types As New List(Of Integer)(capacity)

            Dim neuropilNames As New List(Of String)()
            Dim neuropilIds As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
            Dim ntNames As New List(Of String)()
            Dim ntIds As New Dictionary(Of String, Integer)(StringComparer.Ordinal)

            Dim rows As Long = 0
            Dim skipped As Long = 0
            Dim truncated As Boolean = False

            For Each row As Connections In m_config.ResolvePath(m_config.ConnectionsCsv).StreamConnections()
                Call throwIfCancelled(cancel)

                rows += 1

                If (rows Mod 200000) = 0 Then
                    Call report(progress, $"  {rows} connection rows, {pre.Count} accepted ...")
                End If

                Dim preIndex As Integer
                Dim postIndex As Integer

                ' 索引在注释表建立之后已经冻结，连接表里出现的其它 root_id 只能跳过
                If Not index.IndexOf(row.PreRootId, preIndex) Then
                    skipped += 1
                    Continue For
                End If
                If Not index.IndexOf(row.PostRootId, postIndex) Then
                    skipped += 1
                    Continue For
                End If

                Call pre.Add(preIndex)
                Call post.Add(postIndex)
                Call synapses.Add(CInt(System.Math.Max(0, System.Math.Min(Integer.MaxValue, row.SynCount))))
                Call neuropils.Add(intern(row.Neuropil, neuropilNames, neuropilIds))
                Call types.Add(intern(row.NtType, ntNames, ntIds))

                If pre.Count >= MaxConnections Then
                    truncated = True
                    Exit For
                End If
            Next

            dataset.Pre = pre.ToArray()
            dataset.Post = post.ToArray()
            dataset.SynCount = synapses.ToArray()
            dataset.ConnectionNeuropil = neuropils.ToArray()
            dataset.ConnectionNtType = types.ToArray()
            dataset.ConnectionNeuropils = neuropilNames.ToArray()
            dataset.ConnectionNeurotransmitters = ntNames.ToArray()

            Call report(progress, $"  {rows} rows -> {dataset.ConnectionCount} connections " &
                                  $"({skipped} skipped, {neuropilNames.Count} neuropils, {ntNames.Count} neurotransmitters)")

            If truncated Then
                Call report(progress, $"  [WARN] the connection table was truncated at {MaxConnections} rows")
            End If
        End Sub

        ''' <summary>
        ''' 仿真活跃度：读取 ``neuron_activity_*.csv`` (全量逐神经元脉冲计数)。
        ''' </summary>
        ''' <remarks>
        ''' 找不到时不做任何事：界面可以在需要的时候现场跑一次全脑仿真 (GPU 下只要几十毫秒)，
        ''' 那条路径拿到的活跃度与这里的文件完全同源 (都是 <c>BrainSimulationResult.Counts</c>)。
        ''' </remarks>
        Private Sub loadActivity(dataset As BrainDataset, progress As Action(Of String))
            Dim dir As String = m_config.ResolveActivityDir()

            If Not Directory.Exists(dir) Then
                Return
            End If

            Dim latest As String = Directory _
                .EnumerateFiles(dir, "neuron_activity_*.csv") _
                .OrderByDescending(Function(f) File.GetLastWriteTimeUtc(f)) _
                .FirstOrDefault()

            If latest Is Nothing Then
                Call report(progress, $"no activity table found under {dir}")

                Return
            End If

            Dim counts As Double() = New Double(dataset.Units - 1) {}
            Dim accepted As Integer = 0

            ' 表头: neuron_index,root_id,spike_count,firing_rate
            For Each line As String In File.ReadLines(latest).Skip(1)
                If String.IsNullOrWhiteSpace(line) Then
                    Continue For
                End If

                Dim parts As String() = line.Split(","c)

                If parts.Length < 3 Then
                    Continue For
                End If

                Dim i As Integer

                If Not Integer.TryParse(parts(0), i) OrElse i < 0 OrElse i >= counts.Length Then
                    Continue For
                End If

                counts(i) = Double.Parse(parts(2), NumberStyles.Float, CultureInfo.InvariantCulture)
                accepted += 1
            Next

            If accepted > 0 Then
                dataset.Activity = counts
                dataset.ActivitySource = Path.GetFileName(latest)

                Call report(progress, $"  activity loaded from {dataset.ActivitySource} ({accepted} rows)")
            End If
        End Sub

#End Region

        ''' <summary>把名称字符串映射到连续索引 (保持首次出现的顺序)。</summary>
        Private Shared Function intern(name As String, names As List(Of String), ids As Dictionary(Of String, Integer)) As Integer
            Dim text As String = If(String.IsNullOrWhiteSpace(name), "(unknown)", name.Trim)
            Dim id As Integer

            If ids.TryGetValue(text, id) Then
                Return id
            End If

            id = names.Count
            Call names.Add(text)
            Call ids.Add(text, id)

            Return id
        End Function

        Private Sub validateFiles()
            Dim required As String() = {
                m_config.NamesCsv,
                m_config.ClassificationCsv,
                m_config.CellTypesCsv,
                m_config.NeuronsCsv,
                m_config.CoordinatesCsv,
                m_config.NeuropilTableCsv,
                m_config.ConnectionsCsv
            }
            Dim missing As New List(Of String)()

            For Each name As String In required
                Dim path As String = m_config.ResolvePath(name)

                ' 注意：循环变量不能叫 file，否则会遮蔽 System.IO.File (VB 不区分大小写)
                If Not IO.File.Exists(path) Then
                    Call missing.Add(path)
                End If
            Next

            If missing.Count > 0 Then
                Throw New FileNotFoundException(
                    $"数据文件缺失 (DataDir = {m_config.DataDir}):{Environment.NewLine}  " &
                    String.Join(Environment.NewLine & "  ", missing))
            End If
        End Sub

        Private Shared Sub throwIfCancelled(cancel As CancellationToken)
            If cancel.IsCancellationRequested Then
                Throw New OperationCanceledException(cancel)
            End If
        End Sub

        Private Shared Sub report(progress As Action(Of String), message As String)
            If progress Is Nothing Then Return

            Call progress(message)
        End Sub

#Region "neuropil table schema"

        ''' <summary>一个脑区指标的取值委托。</summary>
        Private Structure NeuropilColumn
            Public Getter As Func(Of NeuropilSynapseTable, Double)
            Public Region As Integer
        End Structure

        ''' <summary>
        ''' 脑区表的列布局：把 ``input/output synapses in &lt;region&gt;`` 两族列绑定成委托，
        ''' 并按脑区归组。
        ''' </summary>
        Private NotInheritable Class NeuropilSchema

            Private Const InputPrefix As String = "input synapses in "
            Private Const OutputPrefix As String = "output synapses in "

            Public ReadOnly Property Columns As NeuropilColumn()
            Public ReadOnly Property Regions As String()

            Private Sub New(columns As NeuropilColumn(), regions As String())
                Me.Columns = columns
                Me.Regions = regions
            End Sub

            Public Shared Function Create() As NeuropilSchema
                Dim regions As New List(Of String)()
                Dim regionIds As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                Dim columns As New List(Of NeuropilColumn)()
                Dim getterType As Type = GetType(Func(Of NeuropilSynapseTable, Double))

                For Each [property] As PropertyInfo In GetType(NeuropilSynapseTable).GetProperties()
                    Dim column As String = columnName([property])

                    If column Is Nothing Then
                        Continue For
                    End If

                    Dim region As String

                    If column.StartsWith(InputPrefix, StringComparison.OrdinalIgnoreCase) Then
                        region = column.Substring(InputPrefix.Length)
                    ElseIf column.StartsWith(OutputPrefix, StringComparison.OrdinalIgnoreCase) Then
                        region = column.Substring(OutputPrefix.Length)
                    Else
                        ' 4 个总计列 (input synapses / output partners / ...) 不参与脑区归类
                        Continue For
                    End If

                    Dim regionIndex As Integer

                    If Not regionIds.TryGetValue(region, regionIndex) Then
                        regionIndex = regions.Count
                        Call regions.Add(region)
                        Call regionIds.Add(region, regionIndex)
                    End If

                    Dim accessor As MethodInfo = [property].GetGetMethod()

                    If accessor Is Nothing Then
                        Continue For
                    End If

                    Call columns.Add(New NeuropilColumn With {
                        .Getter = DirectCast(accessor.CreateDelegate(getterType), Func(Of NeuropilSynapseTable, Double)),
                        .Region = regionIndex
                    })
                Next

                Return New NeuropilSchema(columns.ToArray(), regions.ToArray())
            End Function

            Private Shared Function columnName([property] As PropertyInfo) As String
                Dim attributes As Object() = [property].GetCustomAttributes(GetType(ColumnAttribute), False)

                If attributes Is Nothing OrElse attributes.Length = 0 Then
                    Return Nothing
                End If

                Return DirectCast(attributes(0), ColumnAttribute).Name
            End Function

        End Class

#End Region

    End Class

End Namespace
