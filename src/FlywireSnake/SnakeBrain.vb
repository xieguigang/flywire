Imports System.Diagnostics
Imports System.Linq
Imports FlywireAI.Connectome
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports tf = Microsoft.VisualBasic.MachineLearning.TensorFlow

Namespace FlywireSnake

    ''' <summary>
    ''' 果蝇大脑的"游戏模式"：把感觉通道的电流注入<b>感觉神经元</b>，逐 tick 推进一步，
    ''' 再从<b>运动神经元</b>读出活动。
    ''' </summary>
    ''' <remarks>
    ''' <b>神经通路</b>（全部来自这一份真实连接组）：
    ''' <code>
    '''   游戏状态 → 16 个感觉通道 → 注入 afferent（感觉输入）神经元
    '''            → 递归脉冲网络（sparse LIF，每 tick 一步）
    '''            → 读出 efferent（运动 / 下行输出）神经元的放电率
    ''' </code>
    ''' 
    ''' 这两类神经元不是随便挑的：<c>classification.csv</c> 的 ``flow`` 列把全脑 139,255 个神经元
    ''' 分成 ``afferent``（5,536 个感觉输入）/ ``efferent``（441 个运动输出）/ ``intrinsic``，
    ''' 正好对应"刺激哪里"与"从哪里读出"。
    ''' 
    ''' <b>与刺激实验的区别</b>：刺激实验是"跑固定步数然后看结果"，这里需要"每 tick 推进一步、
    ''' 每步都读数" —— 因此直接调用 <see cref="SparseLIFLayer.ForwardStep"/>，
    ''' 状态在整局游戏里持续累积，只在开局时 <see cref="ResetEpisode"/> 一次。
    ''' </remarks>
    Public Class SnakeBrain

        Private ReadOnly m_index As ConnectomeIndex
        Private ReadOnly m_layer As SparseLIFLayer

        ''' <summary>外部电流张量 [1, N]（跨 tick 复用同一个缓冲）。</summary>
        Private ReadOnly m_external As tf.Tensor

        Private ReadOnly m_features As Double()
        Private ReadOnly m_groupSpikes As Double()

        Private m_active As Integer() = New Integer() {}
        Private m_ticks As Integer

        ''' <summary>每个感觉通道对应的神经元索引。</summary>
        Public ReadOnly Property SensorNeurons As Integer()()

        ''' <summary>运动读出分组（默认 4 组，对应 4 个动作方向）。</summary>
        Public ReadOnly Property MotorGroups As Integer()()

        ''' <summary>本 tick 发放过的全部神经元（供三维可视化）。</summary>
        Public ReadOnly Property ActiveNeurons As Integer()
            Get
                Return m_active
            End Get
        End Property

        ''' <summary>每个运动神经元的滑动窗放电率（解码器的特征向量，长度 = 运动神经元数）。</summary>
        Public ReadOnly Property MotorFeatures As Double()
            Get
                Return m_features
            End Get
        End Property

        ''' <summary>每个运动读出组的本 tick 脉冲数。</summary>
        Public ReadOnly Property MotorGroupSpikes As Double()
            Get
                Return m_groupSpikes
            End Get
        End Property

        ''' <summary>已推进的 tick 数。</summary>
        Public ReadOnly Property TickCount As Integer
            Get
                Return m_ticks
            End Get
        End Property

        ''' <summary>感觉通道的注入电流标定值（通道强度 1.0 时注入的电流）。</summary>
        Public Property SensorCurrent As Double = 2.4

        ''' <summary>放电率特征的滑动窗宽（tick）。</summary>
        Public Property FeatureWindow As Integer = 4

        ''' <summary>神经元总数。</summary>
        Public ReadOnly Property Units As Integer

        ''' <summary>神经元功能分层统计（诊断）。</summary>
        Public ReadOnly Property FlowSummary As String

        ''' <summary>最近一个 tick 的总发放神经元数。</summary>
        Public ReadOnly Property ActiveCount As Integer
            Get
                Return m_active.Length
            End Get
        End Property

        ''' <summary>
        ''' 在已装配好的网络之上构造"游戏模式"的大脑。
        ''' </summary>
        ''' <param name="index">连接组索引（提供 flow 注释）</param>
        ''' <param name="network">已装配的稀疏递归网络（<c>BrainNetworkBuilder.Assemble</c>）</param>
        ''' <param name="sensorsPerChannel">每个感觉通道使用多少个感觉神经元</param>
        ''' <param name="motorGroups">运动读出分组数（= 动作数）</param>
        ''' <param name="seed">挑选神经元的随机种子（保证可复现）</param>
        ''' <remarks>
        ''' 装配与增益标定（读连接表、建 CSR）是一次性的工作，由 <see cref="SnakePlayground"/> 完成；
        ''' 本类只负责"选神经元 + 逐步推进"。
        ''' </remarks>
        ''' <remarks>
        ''' 形参不能叫 <c>motorGroups</c>：VB 不区分大小写，会遮蔽 <see cref="MotorGroups"/> 属性，
        ''' 于是下面所有 <c>MotorGroups.Length</c> 都会变成对整数取 Length。
        ''' </remarks>
        Public Sub New(index As ConnectomeIndex,
                       network As BrainNetwork,
                       Optional sensorsPerChannel As Integer = 32,
                       Optional groupCount As Integer = SnakeSensorEncoder.ActionCount,
                       Optional seed As Integer = 42)

            If index Is Nothing Then Throw New ArgumentNullException(NameOf(index))
            If network Is Nothing Then Throw New ArgumentNullException(NameOf(network))

            m_index = index
            m_layer = network.Network.SparseLayer

            If m_layer Is Nothing Then
                Throw New InvalidOperationException("网络没有装配稀疏递归层，无法用于游戏")
            End If

            Units = network.Units
            m_external = New tf.Tensor(1, Units)

            Dim afferents As Integer() = index.NeuronsOfFlow(ConnectomeIndex.NeuronFlow.Afferent)
            Dim efferents As Integer() = index.NeuronsOfFlow(ConnectomeIndex.NeuronFlow.Efferent)

            If afferents.Length = 0 OrElse efferents.Length = 0 Then
                Throw New InvalidOperationException(
                    "连接组缺少 flow 注释（afferent / efferent），无法建立感觉—运动通路")
            End If

            FlowSummary = $"afferent(感觉输入)={afferents.Length:N0}, efferent(运动输出)={efferents.Length:N0}"

            SensorNeurons = splitChannels(afferents, SnakeSensors.ChannelCount, sensorsPerChannel, seed)
            MotorGroups = splitGroups(efferents, groupCount)

            Dim featureCount As Integer = 0

            For Each group As Integer() In MotorGroups
                featureCount += group.Length
            Next

            m_features = New Double(featureCount - 1) {}
            m_groupSpikes = New Double(MotorGroups.Length - 1) {}

            ' 每 tick 都要读数，因此必须保留逐步脉冲轨迹
            m_layer.KeepHistory = True
            m_layer.UseFusedStep = True
        End Sub

#Region "推进"

        ''' <summary>开局复位（膜电位 / 脉冲 / 计数清零）。</summary>
        Public Sub ResetEpisode()
            Call m_layer.ResetState(1)

            For i As Integer = 0 To m_features.Length - 1
                m_features(i) = 0
            Next

            For i As Integer = 0 To m_groupSpikes.Length - 1
                m_groupSpikes(i) = 0
            Next

            m_active = New Integer() {}
            m_ticks = 0
        End Sub

        ''' <summary>
        ''' 推进一个 tick：注入感觉电流 → 网络走一步 → 读出发放活动。
        ''' </summary>
        ''' <param name="sensorValues">16 个感觉通道的强度 [0,1]</param>
        Public Sub Advance(sensorValues As Double())
            Dim current As Double() = m_external.Data

            ' 1) 重新铺设感觉电流：先清零（上一步的电流不能残留），再把通道强度换成电流
            For i As Integer = 0 To current.Length - 1
                current(i) = 0.0
            Next

            If sensorValues IsNot Nothing Then
                Dim channels As Integer = Math.Min(sensorValues.Length, SensorNeurons.Length)

                For channel As Integer = 0 To channels - 1
                    Dim value As Double = sensorValues(channel)

                    If value <= 0 Then Continue For

                    Dim injection As Double = value * SensorCurrent

                    For Each neuron As Integer In SensorNeurons(channel)
                        current(neuron) += injection
                    Next
                Next
            End If

            ' 就地写主机数组后必须声明，否则设备端会继续复用旧副本
            Call m_external.MarkHostModified()

            ' 2) 网络走一步（每 tick 两次内核启动，状态留在显存里）
            Call m_layer.ForwardStep(m_external)

            m_ticks += 1

            ' 3) 读数：本 tick 的脉冲轨迹
            Dim history As List(Of tf.Tensor) = m_layer.SHistory

            If history Is Nothing OrElse history.Count = 0 Then
                Throw New InvalidOperationException("拿不到本 tick 的脉冲轨迹（KeepHistory 被关闭了？）")
            End If

            Call readSpikes(history(history.Count - 1).Data)
        End Sub

        ''' <summary>从本 tick 的脉冲向量里读出运动神经元活动与全体活跃神经元。</summary>
        Private Sub readSpikes(spikes As Double())
            Dim active As New List(Of Integer)(8192)

            For i As Integer = 0 To spikes.Length - 1
                If spikes(i) <> 0 Then Call active.Add(i)
            Next

            m_active = active.ToArray()

            ' 运动组脉冲数
            For g As Integer = 0 To MotorGroups.Length - 1
                Dim count As Double = 0

                For Each neuron As Integer In MotorGroups(g)
                    If spikes(neuron) <> 0 Then count += 1
                Next

                m_groupSpikes(g) = count
            Next

            ' 逐运动神经元的滑动窗放电率（解码器特征）
            Dim history As List(Of tf.Tensor) = m_layer.SHistory
            Dim window As Integer = Math.Max(1, FeatureWindow)
            Dim from As Integer = Math.Max(0, history.Count - window)
            Dim frames As Integer = Math.Max(1, history.Count - from)
            Dim position As Integer = 0

            For g As Integer = 0 To MotorGroups.Length - 1
                For Each neuron As Integer In MotorGroups(g)
                    Dim total As Double = 0

                    For t As Integer = from To history.Count - 1
                        If history(t).Data(neuron) <> 0 Then total += 1
                    Next

                    m_features(position) = total / frames
                    position += 1
                Next
            Next
        End Sub

        ''' <summary>取某个运动读出组的本 tick 脉冲数。</summary>
        Public Function GroupSpikes(group As Integer) As Double
            If group < 0 OrElse group >= m_groupSpikes.Length Then Return 0

            Return m_groupSpikes(group)
        End Function

#End Region

#Region "神经元挑选"

        ''' <summary>
        ''' 把神经元池均分到各个感觉通道（每通道固定数量，池内按种子打乱后切片）。
        ''' </summary>
        ''' <remarks>
        ''' 用打乱切片而不是"按索引取前 N 个"：这份数据集的索引顺序与解剖位置无关，
        ''' 直接取前 N 个会让所有感觉通道共用同一小片神经元（毫无空间分布）。
        ''' </remarks>
        Private Shared Function splitChannels(pool As Integer(),
                                             channels As Integer,
                                             perChannel As Integer,
                                             seed As Integer) As Integer()()

            Dim shuffled As Integer() = CType(pool.Clone(), Integer())
            Dim rng As New Random(seed)

            For i As Integer = shuffled.Length - 1 To 1 Step -1
                Dim j As Integer = rng.Next(i + 1)
                Dim swap As Integer = shuffled(i)

                shuffled(i) = shuffled(j)
                shuffled(j) = swap
            Next

            Dim count As Integer = Math.Min(perChannel, Math.Max(1, shuffled.Length \ channels))
            Dim result As Integer()() = New Integer(channels - 1)() {}

            For c As Integer = 0 To channels - 1
                Dim block As Integer() = New Integer(count - 1) {}

                For k As Integer = 0 To count - 1
                    block(k) = shuffled((c * count + k) Mod shuffled.Length)
                Next

                result(c) = block
            Next

            Return result
        End Function

        ''' <summary>把运动神经元均分成若干读出组（按索引升序切片，保证可复现）。</summary>
        Private Shared Function splitGroups(pool As Integer(), groups As Integer) As Integer()()
            Dim size As Integer = Math.Max(1, pool.Length \ groups)
            Dim result As Integer()() = New Integer(groups - 1)() {}

            For g As Integer = 0 To groups - 1
                Dim from As Integer = g * size
                Dim count As Integer = Math.Min(size, pool.Length - from)

                If count <= 0 Then
                    result(g) = New Integer() {}
                    Continue For
                End If

                Dim block As Integer() = New Integer(count - 1) {}

                For k As Integer = 0 To count - 1
                    block(k) = pool(from + k)
                Next

                result(g) = block
            Next

            Return result
        End Function

#End Region

    End Class

End Namespace
