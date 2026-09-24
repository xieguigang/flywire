Imports FlywireAI.Connectome
Imports FlywireAI.Connectome.Network
Imports FlywireAI.FAFBv783
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports Snake2


''' <summary>训练 / 评估报告。</summary>
Public Class SnakeTrainingReport

    ''' <summary>采集到的训练样本数。</summary>
    Public Property Samples As Integer

    ''' <summary>训练集上与教师动作的一致率。</summary>
    Public Property Accuracy As Double

    ''' <summary>未训练解码器（随机读出）的均分。</summary>
    Public Property UntrainedScore As Double

    ''' <summary>教师（贪心）策略的均分 —— 操作逻辑的上限参考。</summary>
    Public Property TeacherScore As Double

    ''' <summary>训练后由果蝇大脑驱动的均分。</summary>
    Public Property TrainedScore As Double

    ''' <summary>评估局数。</summary>
    Public Property Episodes As Integer

    ''' <summary>每局最大 tick 数。</summary>
    Public Property MaxTicks As Integer

    ''' <summary>本阶段耗时 (毫秒)。</summary>
    Public Property ElapsedMs As Long

    ''' <summary>
    ''' 训练好的解码器（界面可以拿它替换正在游玩的解码器，让"训练后的大脑"立刻上场）。
    ''' </summary>
    Public Property TrainedDecoder As SnakeDecoder

    Public Function Describe() As String
        Return $"样本 {Samples:N0}（与教师一致率 {Accuracy:P1}）· 均分：未训练 {UntrainedScore:F1} → " &
               $"训练后 {TrainedScore:F1}（教师上限 {TeacherScore:F1}）· " &
               $"{Episodes} 局 × {MaxTicks} tick · {ElapsedMs:N0} ms"
    End Function

End Class

''' <summary>
''' 果蝇大脑玩贪吃蛇的一站式装配：连接组 → CSR → 网络 → 大脑 → 会话 / 训练 / 评估。
''' </summary>
''' <remarks>
''' <b>为什么要单独有一层</b>：装配（读 261 MB 连接表、建 CSR、标定增益）是一次性、
''' 且与"玩哪一局"无关的工作；<see cref="SnakeBrain"/> 只负责逐 tick 推进，
''' <see cref="SnakeSession"/> 只负责推进游戏。把装配收在这里，界面与命令行
''' 就都能"装一次、玩多局、训练多轮"。
''' </remarks>
Public Class SnakePlayground

    Private ReadOnly m_config As SnnConfig

    ''' <summary>连接组索引（提供 flow / 注释信息）。</summary>
    Public ReadOnly Property Index As ConnectomeIndex

    ''' <summary>CSR 权重矩阵（行 = 突触前，列 = 突触后）。</summary>
    Public ReadOnly Property Matrix As ConnectomeMatrix

    ''' <summary>标定得到的全局权重增益。</summary>
    Public ReadOnly Property Gain As Double

    ''' <summary>数据目录。</summary>
    Public ReadOnly Property DataDir As String

    ''' <summary>装配耗时 (毫秒)。</summary>
    Public ReadOnly Property ElapsedMs As Long

#Region "感觉 / 读出标定"

    ''' <summary>
    ''' 每个感觉通道使用多少个感觉神经元（"电极铺多大"）。
    ''' </summary>
    ''' <remarks>
    ''' 这份连接组做过结构归一化，注入的神经元太少时响应扩散不到运动神经元 ——
    ''' 实测每个通道 256 个（共 4,096 个注入）时，运动神经元平均每个 tick 只有 1.1 个脉冲，
    ''' 读出层拿到的特征几乎是空的（与示范动作的一致率只有 45.7%），
    ''' 蛇看起来就是"没有目标地乱走"。
    ''' 
    ''' 实测对照（8 局 × 300 tick）：
    ''' <code>
    ''' 每通道神经元   运动神经元脉冲/tick   与教师一致率
    '''    256              1.1                45.7%
    '''   1024              8.9                73.3%
    ''' </code>
    ''' 因此默认取 1024（afferent 池 19,300 个，每通道 1,024 个时共铺开 16,384 个）。
    ''' </remarks>
    Public Property SensorsPerChannel As Integer = 1024

    ''' <summary>感觉通道强度为 1.0 时，每个感觉神经元注入的电流。</summary>
    Public Property SensorCurrent As Double = 2.4

    ''' <summary>运动读出特征（滑动窗放电率）的窗宽（tick）。</summary>
    Public Property FeatureWindow As Integer = 4

    ''' <summary>决策迟滞的相对余量（占打分跨度的比例；0 = 不迟滞）。</summary>
    Public Property DecisionMargin As Double = 0.15

    ''' <summary>决策打分的滑动平均系数。</summary>
    Public Property DecisionSmoothing As Double = 0.45

    ''' <summary>
    ''' 决策保持的 tick 数（转向冷却；1 = 每个 tick 都可以改主意）。
    ''' </summary>
    ''' <remarks>
    ''' 实测（8 局训练 + <b>40 局</b>评估，越低方差越可信）：
    ''' <code>
    ''' 保持 tick   闭环均分
    '''    1          5.65
    '''    3          9.20
    '''    5          7.62
    ''' </code>
    ''' 读出层逐 tick 有 ~30% 的概率与示范动作不一致，而贪吃蛇里一次错误转向就可能致命；
    ''' "保持 3 tick"把误差累积速度直接降到三分之一，是这一轮里提升最明显的一个旋钮。
    ''' </remarks>
    Public Property DecisionHoldTicks As Integer = 3

    ''' <summary>
    ''' DAgger 聚合轮数（0 = 只做一次教师示范就训练，不做误差累积修正）。
    ''' </summary>
    ''' <remarks>
    ''' 只用教师示范的样本训练，读出层上场后会偏离到教师没走过的状态上（covariate shift）：
    ''' 实测与教师的一致率 73%，但真正上场只有 5.6 分。DAgger 让读出层自己走、
    ''' 教师在路上继续标注，把这些"自己走出来的状态"聚合进训练集重新训练。
    ''' </remarks>
    Public Property DaggerRounds As Integer = 2

    ''' <summary>
    ''' 强化学习微调的局数（0 = 不做）。
    ''' </summary>
    ''' <remarks>
    ''' 默认关闭：实测这一层在当前规模下没能稳定地把闭环得分抬起来
    ''' （同一版权重在两次 8 局评估里分别是 15.6 与 6.3，游戏本身单局方差太大，
    ''' 40 局评估下模仿学习本身只有 3.75 分）。代码留着，
    ''' 等读出层的信息量上来（或把评估局数做大）再开。
    ''' </remarks>
    Public Property RlEpisodes As Integer = 0

    ''' <summary>强化学习每练几局评一次分（并只保留最好的一版）。</summary>
    Public Property RlBlockSize As Integer = 20

    ''' <summary>
    ''' 强化学习期间每次评分的局数。
    ''' </summary>
    ''' <remarks>
    ''' 这个游戏单局得分的方差很大（同一版策略实测在 0~12 分之间跳），
    ''' 局数太少的话"保留最好的一版"基本等于掷骰子。
    ''' </remarks>
    Public Property RlEvaluationEpisodes As Integer = 8

    ''' <summary>强化学习的学习率（按决策步数平摊后的每步步长）。</summary>
    Public Property RlLearningRate As Double = 0.0005

#End Region

    ''' <remarks>
    ''' 形参名刻意避开 <c>index</c> / <c>matrix</c> / <c>gain</c>：VB 不区分大小写，
    ''' 与同名属性（<see cref="Index"/> / <see cref="Matrix"/> / <see cref="Gain"/>）
    ''' 撞上之后 <c>Index = index</c> 会退化成"参数给自己赋值"，
    ''' 属性永远是 Nothing（第一次用到就炸）。
    ''' </remarks>
    Private Sub New(config As SnnConfig, connectome As ConnectomeIndex, csr As ConnectomeMatrix,
                    globalGain As Double, elapsed As Long)

        m_config = config
        Index = connectome
        Matrix = csr
        Gain = globalGain
        DataDir = config.DataDir
        ElapsedMs = elapsed
    End Sub

#Region "装配"

    ''' <summary>
    ''' 装配连接组并标定一次增益（耗时数十秒：主要是读连接表与建 CSR）。
    ''' </summary>
    ''' <param name="config">SNN 配置（提供数据文件路径、增益标定参数、后端开关）</param>
    ''' <param name="reporter">进度回调</param>
    Public Shared Function Create(config As SnnConfig, Optional reporter As Action(Of String) = Nothing) As SnakePlayground
        If config Is Nothing Then Throw New ArgumentNullException(NameOf(config))

        Dim timer As Stopwatch = Stopwatch.StartNew()

        ' 1) 神经元索引：names.csv 给出全脑神经元，再叠加连接表里出现的神经元
        Call report(reporter, $"loading cell names from {config.NamesCsv} ...")

        Dim names As List(Of CellNames) = config.ResolvePath(config.NamesCsv).LoadCellNames()
        Dim index As New ConnectomeIndex()

        For Each cell As CellNames In names
            Call index.Add(cell.RootId)
        Next

        ' 2) 连接三元组 → CSR（连接表的端点全部在 names.csv 内，因此索引可以安全共享）
        Call report(reporter, $"building the connectome from {config.ConnectionsCsv} ...")

        Dim triplets As SynapseTriplets = SynapseTriplets.Build(
            index,
            config.ResolvePath(config.ConnectionsCsv),
            config.ExcitatoryGain,
            config.InhibitoryGain)

        Call index.Freeze()

        ' 3) 注释（其中 classification 的 flow 列决定"感觉输入 / 运动输出"这两组神经元）
        Call index.AttachAnnotations(
            names,
            config.ResolvePath(config.ClassificationCsv).LoadClassification(),
            config.ResolvePath(config.CellTypesCsv).LoadCellTypes(),
            config.ResolvePath(config.NeuronsCsv).LoadNeurons())

        Dim matrix As ConnectomeMatrix = BrainNetworkBuilder.BuildMatrix(triplets, reporter)

        Call report(reporter, $"connectome ready: {matrix} ({timer.ElapsedMilliseconds} ms)")

        ' 4) 增益标定：用批量刺激方案标定一次，之后固定使用
        Dim gain As Double = If(config.GlobalGain > 0, config.GlobalGain, 1.0)

        If config.GlobalGain <= 0 Then
            Dim mass As Stimulation = Stimulation.Create(config, index)
            Dim probe As BrainNetwork = BrainNetworkBuilder.Assemble(config, matrix, mass, reporter)

            gain = BrainSimulation.CalibrateGain(probe, config, mass, reporter)
            Call releaseDeviceBuffers(probe)
        End If

        config.GlobalGain = gain

        Call report(reporter, $"playground ready: gain={gain}, elapsed={timer.ElapsedMilliseconds} ms")

        Return New SnakePlayground(config, index, matrix, gain, timer.ElapsedMilliseconds)
    End Function

    Private Shared Sub releaseDeviceBuffers(network As BrainNetwork)
        If network Is Nothing OrElse network.Network Is Nothing Then Return

        Dim layer As SparseLIFLayer = network.Network.SparseLayer

        If layer IsNot Nothing Then
            Try
                Call layer.ReleaseDeviceBuffers()
            Catch ex As Exception
                System.Diagnostics.Debug.WriteLine($"unable to release resident buffers: {ex.Message}")
            End Try
        End If
    End Sub

    Private Shared Sub report(reporter As Action(Of String), message As String)
        If reporter Is Nothing Then Return

        Call reporter(message)
    End Sub

#End Region

#Region "大脑 / 会话"

    ''' <summary>
    ''' 装配一个"游戏模式"的果蝇大脑（每次调用都是一份独立的状态，可以并行跑多局）。
    ''' </summary>
    ''' <param name="seed">挑选神经元的随机种子</param>
    ''' <remarks>
    ''' 网络装配本身很轻（<c>AddSparseLayer</c> 只持有 CSR 引用），
    ''' 因此"每局一份大脑"的代价可以忽略；真正的重活是连接组装配（见 <see cref="Create"/>）。
    ''' 
    ''' "电极铺多大 / 注入多强 / 读出窗多宽"这三件事的口径统一由
    ''' <see cref="SensorsPerChannel"/> / <see cref="SensorCurrent"/> / <see cref="FeatureWindow"/>
    ''' 决定：训练（<see cref="Train"/>）、评估（<see cref="Evaluate"/>）与观战窗口
    ''' 都走这一个入口，免得"训练时一套标定、玩的时候另一套"。
    ''' </remarks>
    Public Function CreateBrain(Optional seed As Integer = 42) As SnakeBrain

        ' 这一份 Stimulation 只是给"输入特征"占个位：
        ' 游戏里的输入是每 tick 直接写进外部电流张量的（见 SnakeBrain.Advance），
        ' 不经过 Network.ForwardSparse 的散射路径。
        Dim bootstrap As Stimulation = Stimulation.CreateSingle(Index, 0, 1.0, "snake sensors")
        Dim network As BrainNetwork = BrainNetworkBuilder.Assemble(m_config, Matrix, bootstrap, Nothing)
        Dim brain As New SnakeBrain(Index, network, SensorsPerChannel, SnakeSensorEncoder.ActionCount, seed)

        brain.SensorCurrent = SensorCurrent
        brain.FeatureWindow = FeatureWindow

        Return brain
    End Function

    ''' <summary>
    ''' 构造一次游戏会话。
    ''' </summary>
    ''' <param name="brain">果蝇大脑</param>
    ''' <param name="decoder">运动解码器</param>
    ''' <param name="host">
    ''' 承载游戏画面尺寸的窗体（游戏的食物生成依赖相机可视范围）。
    ''' 界面里传自己的观战窗口；命令行 / 训练时传 Nothing，内部会用一个隐藏窗体代替。
    ''' </param>
    Public Function CreateSession(brain As SnakeBrain,
                                  decoder As SnakeDecoder,
                                  Optional host As Form = Nothing) As SnakeSession

        ' 局部变量不能叫 game：VB 不区分大小写，会遮蔽类型名 Game（下面的 Game.ViewCols 就取不到了）
        Dim canvas As Form = If(host, New Form() With {
            .ClientSize = New Size(Snake2.Game.ViewCols * Snake2.Game.CellSize,
                                   Snake2.Game.ViewRows * Snake2.Game.CellSize)
        })

        Dim world As New Snake2.Game()
        Dim render As New Render(canvas, world)

        Call world.InitGame(render)

        Dim session As New SnakeSession(world, render, brain, decoder)

        ' 决策侧的标定与装配同源：训练、评估、观战窗口用的是同一套迟滞 / 平滑参数
        session.DecisionFilter.Margin = DecisionMargin
        session.DecisionFilter.Smoothing = DecisionSmoothing
        session.DecisionFilter.HoldTicks = Math.Max(1, DecisionHoldTicks)

        Return session
    End Function

#End Region

#Region "训练 / 评估"

    ''' <summary>
    ''' 训练一个能驱动贪吃蛇的解码器：先记录教师策略的演示，再训练线性读出。
    ''' </summary>
    ''' <param name="episodes">采集用的局数（同时用于三组评估）</param>
    ''' <param name="maxTicks">每局最大 tick 数</param>
    ''' <param name="reporter">进度回调</param>
    ''' <param name="seed">解码器初始化种子</param>
    Public Function Train(Optional episodes As Integer = 8,
                          Optional maxTicks As Integer = 300,
                          Optional reporter As Action(Of String) = Nothing,
                          Optional seed As Integer = 20180923) As SnakeTrainingReport

        Dim timer As Stopwatch = Stopwatch.StartNew()
        Dim brain As SnakeBrain = CreateBrain(seed:=seed)
        Dim decoder As New SnakeDecoder(brain.MotorFeatures.Length, seed)

        Call report(reporter, $"brain ready: {brain.FlowSummary}, motor neurons={brain.MotorFeatures.Length}")

        ' ---- 对照 1：未训练解码器（随机读出）----
        Dim untrained As Double = averageScore(decoder, brain, episodes, maxTicks, teacher:=False)

        Call report(reporter, $"untrained decoder score = {untrained:F2}")

        ' ---- 对照 2：教师策略（贪心追食物），训练目标的上限参考 ----
        Dim teacherScore As Double = averageScore(decoder, brain, episodes, maxTicks, teacher:=True)

        Call report(reporter, $"teacher (greedy) score = {teacherScore:F2}")

        ' ---- 采集演示样本（DAgger：先示范、再聚合）----
        ' 只采集教师示范的话，训练集里全是"教师走出来的状态"；读出层上场后会偏离到教师
        ' 没走过的状态上（模仿学习的误差累积，covariate shift），
        ' 实测一致率 73% 而真正上场只有 5.6 分。DAgger 的做法是：
        ' 让读出层自己走、教师沿路继续标注，把这些状态聚合进训练集反复重训。
        Dim samples As New List(Of SnakeSample)(episodes * maxTicks * (1 + Math.Max(0, DaggerRounds)))
        Dim session As SnakeSession = CreateSession(brain, decoder)

        For e As Integer = 1 To episodes
            Call session.RunEpisode(maxTicks, samples, markTeacher:=True)
        Next

        Call report(reporter, $"teacher demonstration: samples={samples.Count:N0}")

        Dim accuracy As Double = decoder.Train(samples, epochs:=24, rate:=0.25)

        Call report(reporter, $"decoder trained (round 0): accuracy={accuracy:P1}")
        Call report(reporter, decoder.Diagnose(samples))

        For round As Integer = 1 To Math.Max(0, DaggerRounds)
            For e As Integer = 1 To episodes
                ' markTeacher:=False → 由读出层自己决定走向，但样本标签仍然是教师动作
                Call session.RunEpisode(maxTicks, samples, markTeacher:=False)
            Next

            accuracy = decoder.Train(samples, epochs:=24, rate:=0.25)

            Call report(reporter,
                        $"decoder trained (dagger {round}): samples={samples.Count:N0}, accuracy={accuracy:P1}")
            Call report(reporter, decoder.Diagnose(samples))
        Next

        ' ---- 强化学习微调：不再"像教师"，而是直接把分数拿回来 ----
        If RlEpisodes > 0 Then
            Dim policy As New SnakePolicyGradient(decoder) With {.LearningRate = RlLearningRate}
            Dim bestReinforce As Double = policy.Train(
                session,
                RlEpisodes,
                RlBlockSize,
                maxTicks,
                Function() averageScore(decoder, brain, Math.Max(1, RlEvaluationEpisodes), maxTicks, False),
                Sub(message) Call report(reporter, message))

            Call report(reporter,
                        $"reinforce done: best closed-loop score = {bestReinforce:F2} " &
                        $"（{policy.Episodes} 局，权重已回退到最好的一版）")
        End If

        ' ---- 对照 3：训练后由果蝇大脑驱动 ----
        Dim trained As Double = averageScore(decoder, brain, episodes, maxTicks, teacher:=False)

        Call report(reporter, $"trained decoder score = {trained:F2}")

        timer.Stop()

        Return New SnakeTrainingReport With {
            .Samples = samples.Count,
            .Accuracy = accuracy,
            .UntrainedScore = untrained,
            .TeacherScore = teacherScore,
            .TrainedScore = trained,
            .Episodes = episodes,
            .MaxTicks = maxTicks,
            .ElapsedMs = timer.ElapsedMilliseconds,
            .TrainedDecoder = decoder
        }
    End Function

    ''' <summary>跑若干局并返回平均得分。</summary>
    Public Function Evaluate(decoder As SnakeDecoder,
                             brain As SnakeBrain,
                             Optional episodes As Integer = 5,
                             Optional maxTicks As Integer = 300,
                             Optional teacher As Boolean = False) As Double

        Return averageScore(decoder, brain, episodes, maxTicks, teacher)
    End Function

    ''' <remarks>
    ''' 名字不能叫 <c>evaluate</c>：VB 不区分大小写，那样就与公开的
    ''' <see cref="Evaluate"/> 只差"可选参数"了，编译器不允许这种重载。
    ''' </remarks>
    Private Function averageScore(decoder As SnakeDecoder,
                              brain As SnakeBrain,
                              episodes As Integer,
                              maxTicks As Integer,
                              teacher As Boolean) As Double

        Dim session As SnakeSession = CreateSession(brain, decoder)
        Dim total As Double = 0

        For e As Integer = 1 To Math.Max(1, episodes)
            Dim result As SnakeEpisodeResult = session.RunEpisode(maxTicks, collect:=Nothing, markTeacher:=teacher)

            total += result.Score
        Next

        Return total / Math.Max(1, episodes)
    End Function

#End Region

End Class

