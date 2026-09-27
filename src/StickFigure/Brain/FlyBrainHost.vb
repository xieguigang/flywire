Imports System.Diagnostics
Imports System.IO
Imports FlywireAI.Connectome
Imports FlywireAI.Connectome.Network
Imports FlywireAI.FAFBv783
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork

''' <summary>
''' 果蝇全脑连接组的装配宿主：读数据 → 建连接组 → 标定增益 → 产出 <see cref="FlyBrain"/>。
''' </summary>
''' <remarks>
''' <para>
''' 对应贪吃蛇 demo 里的 <c>FlywireSnake.SnakePlayground.Create</c>，去掉了贪吃蛇的
''' 会话 / 训练部分，只保留"装配 + 造大脑"。装配（读连接表、建 CSR、标定增益）
''' 需要<b>数十秒</b>，因此由 UI 的接管开关或 CLI 命令显式触发，绝不能放在启动路径上。
''' </para>
''' <para>
''' 数据来源二选一：FAFB v783 的 csv 目录（<see cref="SnnConfig.DataDir"/>），
''' 或者 <see cref="SnnConfig.PackFile"/> 指向的 msgpack 转储包（给了包就只从包里取数）。
''' </para>
''' </remarks>
Public Class FlyBrainHost

    ''' <summary>连接组索引（提供 flow / 注释信息）。</summary>
    Public ReadOnly Property Index As ConnectomeIndex

    ''' <summary>CSR 权重矩阵（行 = 突触前，列 = 突触后）。</summary>
    Public ReadOnly Property Matrix As ConnectomeMatrix

    ''' <summary>标定得到的全局权重增益。</summary>
    Public ReadOnly Property Gain As Double

    ''' <summary>数据目录。</summary>
    Public ReadOnly Property DataDir As String

    ''' <summary>装配耗时（毫秒）。</summary>
    Public ReadOnly Property ElapsedMs As Long

    ''' <summary>装配使用的配置。</summary>
    Public ReadOnly Property Config As SnnConfig

    Private ReadOnly m_pack As FafbPackReader

    Private Sub New(snnConfig As SnnConfig,
                    connectome As ConnectomeIndex,
                    csr As ConnectomeMatrix,
                    globalGain As Double,
                    elapsed As Long,
                    pack As FafbPackReader)

        ' 形参名不能叫 config：VB 不区分大小写，Config = config 会退化成自赋值
        Config = snnConfig
        Index = connectome
        Matrix = csr
        Gain = globalGain
        DataDir = snnConfig.DataDir
        ElapsedMs = elapsed
        m_pack = pack
    End Sub

#Region "感觉 / 读出标定"

    ''' <summary>
    ''' 每个感觉通道使用多少个感觉神经元（"电极铺多大"）。
    ''' </summary>
    ''' <remarks>
    ''' 这份连接组做过结构归一化，注入的神经元太少时响应扩散不到运动神经元：
    ''' 贪吃蛇 demo 的实测数据是每通道 256 个时运动神经元每 tick 只有 1.1 个脉冲
    ''' （读出层拿到的特征几乎是空的），1024 个时 8.9 个、与教师一致率从 45.7% 升到 73.3%。
    ''' 因此这里同样默认 1024。
    ''' </remarks>
    Public Property SensorsPerChannel As Integer = 1024

    ''' <summary>感觉通道强度为 1.0 时，每个感觉神经元注入的电流。</summary>
    Public Property SensorCurrent As Double = 2.4

    ''' <summary>运动读出特征（滑动窗放电率）的窗宽（tick）。</summary>
    Public Property FeatureWindow As Integer = 4

#End Region

#Region "装配"

    ''' <summary>
    ''' 装配连接组并标定一次增益。
    ''' </summary>
    ''' <param name="config">SNN 配置（数据文件路径、增益标定参数、后端开关）</param>
    ''' <param name="reporter">进度回调</param>
    ''' <param name="pack">
    ''' msgpack 转储包：给了它就<b>只从包里取数</b>，不再读任何 csv。
    ''' 调用方负责打开与关闭（本函数不接管它的生命周期）。
    ''' </param>
    Public Shared Function Create(config As SnnConfig,
                                  Optional reporter As Action(Of String) = Nothing,
                                  Optional pack As FafbPackReader = Nothing) As FlyBrainHost
        If config Is Nothing Then Throw New ArgumentNullException(NameOf(config))

        Dim timer As Stopwatch = Stopwatch.StartNew()

        ' 1) 神经元索引：主索引来自 names，再叠加连接表里出现的神经元
        Dim names As List(Of CellNames)

        If pack IsNot Nothing Then
            Call report(reporter, $"loading cell names from {FafbMsgPackStorage.EntryNameOf(FafbMsgPackStorage.KeyNames)} ...")

            names = packNames(pack)
        Else
            Call report(reporter, $"loading cell names from {config.NamesCsv} ...")

            names = config.ResolvePath(config.NamesCsv).LoadCellNames()
        End If

        Dim connectome As New ConnectomeIndex()

        For Each cell As CellNames In names
            Call connectome.Add(cell.RootId)
        Next

        ' 2) 连接三元组 → CSR
        Dim triplets As SynapseTriplets

        If pack IsNot Nothing Then
            Call report(reporter, $"building the connectome from {FafbMsgPackStorage.EntryNameOf(FafbMsgPackStorage.KeyConnections)} ...")

            triplets = SynapseTriplets.Build(
                connectome,
                columnOf(Of LongColumnPack)(pack, FafbMsgPackStorage.ConnectionColumnKey(FafbMsgPackStorage.ColumnPre)).Values,
                columnOf(Of LongColumnPack)(pack, FafbMsgPackStorage.ConnectionColumnKey(FafbMsgPackStorage.ColumnPost)).Values,
                columnOf(Of DoubleColumnPack)(pack, FafbMsgPackStorage.ConnectionColumnKey(FafbMsgPackStorage.ColumnSynapses)).Values,
                columnOf(Of IntegerColumnPack)(pack, FafbMsgPackStorage.ConnectionColumnKey(FafbMsgPackStorage.ColumnNt)).Values,
                columnNtNames(pack),
                config.ExcitatoryGain,
                config.InhibitoryGain)
        Else
            Call report(reporter, $"building the connectome from {config.ConnectionsCsv} ...")

            triplets = SynapseTriplets.Build(
                connectome,
                config.ResolvePath(config.ConnectionsCsv),
                config.ExcitatoryGain,
                config.InhibitoryGain)
        End If

        Call connectome.Freeze()

        ' 3) 注释（其中 classification 的 flow 列决定"感觉输入 / 运动输出"这两组神经元）
        If pack IsNot Nothing Then
            Call connectome.AttachAnnotations(
                names,
                packClassification(pack),
                packCellTypes(pack),
                packNeurons(pack))
        Else
            Call connectome.AttachAnnotations(
                names,
                config.ResolvePath(config.ClassificationCsv).LoadClassification(),
                config.ResolvePath(config.CellTypesCsv).LoadCellTypes(),
                config.ResolvePath(config.NeuronsCsv).LoadNeurons())
        End If

        Dim matrix As ConnectomeMatrix = BrainNetworkBuilder.BuildMatrix(triplets, reporter)

        Call report(reporter, $"connectome ready: {matrix} ({timer.ElapsedMilliseconds} ms)")

        ' 4) 增益标定：用批量刺激方案标定一次，之后固定使用
        Dim gain As Double = If(config.GlobalGain > 0, config.GlobalGain, 1.0)

        If config.GlobalGain <= 0 Then
            Dim mass As Stimulation = Stimulation.Create(config, connectome)
            Dim probe As BrainNetwork = BrainNetworkBuilder.Assemble(config, matrix, mass, reporter)

            gain = BrainSimulation.CalibrateGain(probe, config, mass, reporter)
            Call releaseDeviceBuffers(probe)
        End If

        config.GlobalGain = gain

        Call report(reporter, $"fly brain host ready: gain={gain}, elapsed={timer.ElapsedMilliseconds} ms")

        Return New FlyBrainHost(config, connectome, matrix, gain, timer.ElapsedMilliseconds, pack)
    End Function

    ''' <summary>
    ''' 在已装配的连接组之上构造一份新的 <see cref="FlyBrain"/>。
    ''' </summary>
    ''' <remarks>
    ''' 每次调用都是一份<b>独立的状态</b>（独立的膜电位 / 脉冲缓冲），
    ''' 可以并行跑多局或者反复开关 UI 的接管开关。
    ''' </remarks>
    Public Function CreateBrain(Optional seed As Integer = 42) As FlyBrain
        ' 这一份 Stimulation 只是给"输入特征"占个位：
        ' 火柴人的感觉输入是每 tick 直接写进外部电流张量的（见 FlyBrain.Advance），
        ' 不经过 Network.ForwardSparse 的散射路径。
        Dim bootstrap As Stimulation = Stimulation.CreateSingle(Index, 0, 1.0, "stickman sensors")
        Dim network As BrainNetwork = BrainNetworkBuilder.Assemble(Config, Matrix, bootstrap, Nothing)

        ' 连续输出不需要动作分组，groupCount 取 1（全部 efferent 神经元作为一个读出池）
        Dim brain As New FlyBrain(Index, network, SensorsPerChannel, 1, seed)

        brain.SensorCurrent = SensorCurrent
        brain.FeatureWindow = FeatureWindow

        Return brain
    End Function

    ''' <summary>
    ''' 打开 msgpack 转储包（数据消费方用完记得 <see cref="FafbPackReader.Dispose"/>）。
    ''' </summary>
    ''' <param name="packFile">转储包路径；空串则按候选顺序自动查找。</param>
    Public Shared Function OpenPack(packFile As String) As FafbPackReader
        If String.IsNullOrWhiteSpace(packFile) Then
            Return FafbMsgPackStorage.Open(FafbMsgPackStorage.CandidatePackPaths().FirstOrDefault())
        End If

        Return FafbMsgPackStorage.Open(packFile)
    End Function

#End Region

#Region "msgpack 转储包"

    ''' <summary>
    ''' 读回一列；缺列视为致命错误（数据不完整，继续装配只会得到坏结果）。
    ''' </summary>
    ''' <remarks>
    ''' 局部变量不能叫 column（与函数同名），也不能叫 pack（与形参同名）：
    ''' VB 不区分大小写，同名就等于把形参/函数本身遮蔽掉。
    ''' </remarks>
    Private Shared Function columnOf(Of T As Class)(pack As FafbPackReader, key As String) As T
        Dim value As T = pack.Read(Of T)(key)

        If value Is Nothing Then
            Throw New InvalidDataException($"msgpack 转储包缺少数据列: {key}")
        End If

        Return value
    End Function

    Private Shared Function packNames(pack As FafbPackReader) As List(Of CellNames)
        Dim namesPack As CellNamesPack = pack.Read(Of CellNamesPack)(FafbMsgPackStorage.KeyNames)

        Return If(namesPack Is Nothing, New List(Of CellNames)(), namesPack.ToRecords())
    End Function

    Private Shared Function packClassification(pack As FafbPackReader) As List(Of Classification)
        Dim classificationPack As ClassificationPack = pack.Read(Of ClassificationPack)(FafbMsgPackStorage.KeyClassification)

        Return If(classificationPack Is Nothing, New List(Of Classification)(), classificationPack.ToRecords())
    End Function

    Private Shared Function packCellTypes(pack As FafbPackReader) As List(Of CellTypes)
        Dim cellTypesPack As CellTypesPack = pack.Read(Of CellTypesPack)(FafbMsgPackStorage.KeyCellTypes)

        Return If(cellTypesPack Is Nothing, New List(Of CellTypes)(), cellTypesPack.ToRecords())
    End Function

    Private Shared Function packNeurons(pack As FafbPackReader) As List(Of Neurons)
        Dim neuronsPack As NeuronsPack = pack.Read(Of NeuronsPack)(FafbMsgPackStorage.KeyNeurons)

        Return If(neuronsPack Is Nothing, New List(Of Neurons)(), neuronsPack.ToRecords())
    End Function

    Private Shared Function columnNtNames(pack As FafbPackReader) As String()
        Dim connectionsPack As ConnectionsPack = pack.Read(Of ConnectionsPack)(FafbMsgPackStorage.KeyConnections)

        Return If(connectionsPack Is Nothing, Nothing, connectionsPack.NtNames)
    End Function

#End Region

    Private Shared Sub releaseDeviceBuffers(network As BrainNetwork)
        If network Is Nothing OrElse network.Network Is Nothing Then Return

        Dim layer As SparseLIFLayer = network.Network.SparseLayer

        If layer IsNot Nothing Then
            Try
                Call layer.ReleaseDeviceBuffers()
            Catch ex As Exception
                Call System.Diagnostics.Debug.WriteLine($"unable to release resident buffers: {ex.Message}")
            End Try
        End If
    End Sub

    Private Shared Sub report(reporter As Action(Of String), message As String)
        If reporter IsNot Nothing Then
            Call reporter(message)
        End If
    End Sub

End Class
