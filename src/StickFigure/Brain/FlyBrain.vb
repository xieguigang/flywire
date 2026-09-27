Imports System.Math
Imports FlywireAI.Connectome
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports tf = Microsoft.VisualBasic.MachineLearning.TensorFlow

''' <summary>
''' 果蝇全脑 SNN 的"火柴人模式"驱动器。
''' </summary>
''' <remarks>
''' <para>
''' 照搬 <c>FlywireSnake.SnakeBrain</c> 的算法，去掉贪吃蛇语义：
''' </para>
''' <para>
''' 刺激实验是"跑固定步数然后看结果"，而闭环控制需要"每 tick 推进一步、每步都读数"——
''' 因此这里直接调用 <see cref="SparseLIFLayer.ForwardStep"/>，
''' 状态在整个episode里持续累积，只在开局 <see cref="ResetEpisode"/> 一次。
''' </para>
''' <para>
''' 感觉输入是 <see cref="FlyChannels.ChannelCount"/> 个 [0,1] 的通道强度
''' （<see cref="FlySensorEncoder.Encode"/> 编码），每通道的强度 × <see cref="SensorCurrent"/>
''' 的电流均摊到该通道的一组 afferent 神经元上；输出是全部 efferent（运动）神经元的
''' <b>滑动窗放电率向量</b>（<see cref="MotorFeatures"/>），交给
''' <see cref="FlyRegressor"/> 回归成 16 维关节角度偏置。
''' </para>
''' <para>
''' 装配与增益标定（读连接表、建 CSR）是一次性的工作，由 <see cref="FlyBrainHost"/> 完成；
''' 本类只负责"选神经元 + 逐步推进"。
''' </para>
''' </remarks>
Public Class FlyBrain
    Implements IDisposable

    Private ReadOnly m_index As ConnectomeIndex
    Private ReadOnly m_layer As SparseLIFLayer

    ''' <summary>外部电流张量 [1, N]（跨 tick 复用同一个缓冲）。</summary>
    Private ReadOnly m_external As tf.Tensor

    Private ReadOnly m_features As Double()
    Private ReadOnly m_groupSpikes As Double()

    Private m_active As Integer() = New Integer() {}
    Private m_ticks As Integer
    Private m_disposed As Boolean

    ''' <summary>每个感觉通道对应的神经元索引。</summary>
    Public ReadOnly Property SensorNeurons As Integer()()

    ''' <summary>运动神经元分组（连续输出下仅用于统计，默认 1 组）。</summary>
    Public ReadOnly Property MotorGroups As Integer()()

    ''' <summary>本 tick 发放过的全部神经元（供三维可视化）。</summary>
    Public ReadOnly Property ActiveNeurons As Integer()
        Get
            Return m_active
        End Get
    End Property

    ''' <summary>每个运动神经元的滑动窗放电率（回归器的特征向量，长度 = 运动神经元数）。</summary>
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

    ''' <summary>本 tick 运动神经元的活跃数量。</summary>
    Public Property MotorActiveCount As Integer = 0

    ''' <summary>最近一个 tick 实际走的单步实现路径（Fused = 融合内核，OpByOp = 逐算子回退）。</summary>
    Public ReadOnly Property LastStepPath As String
        Get
            If m_layer Is Nothing Then Return ""

            Return m_layer.LastStepPath.ToString
        End Get
    End Property

    ''' <summary>最近一个 tick 里运动神经元的总脉冲数。</summary>
    Public ReadOnly Property MotorActiveSpikes As Double
        Get
            Dim total As Double = 0

            For Each spikes As Double In m_groupSpikes
                total += spikes
            Next

            Return total
        End Get
    End Property

    ''' <summary>
    ''' 在已装配好的网络之上构造"火柴人模式"的大脑。
    ''' </summary>
    ''' <param name="indexOfConnectome">连接组索引（提供 flow 注释）</param>
    ''' <param name="network">已装配的稀疏递归网络（<c>BrainNetworkBuilder.Assemble</c>）</param>
    ''' <param name="sensorsPerChannel">每个感觉通道使用多少个感觉神经元</param>
    ''' <param name="groupCount">运动神经元分组数（连续输出下取 1 即可）</param>
    ''' <param name="seed">挑选神经元的随机种子（保证可复现）</param>
    ''' <remarks>
    ''' 形参名刻意用 <c>indexOfConnectome</c> / <c>groupCount</c>：
    ''' VB 不区分大小写，<c>index</c> / <c>motorGroups</c> 会遮蔽同名属性，
    ''' 让所有 <c>Index = index</c> 退化成"参数给自己赋值"。
    ''' </remarks>
    Public Sub New(indexOfConnectome As ConnectomeIndex,
                   network As BrainNetwork,
                   Optional sensorsPerChannel As Integer = 1024,
                   Optional groupCount As Integer = 1,
                   Optional seed As Integer = 42)

        If indexOfConnectome Is Nothing Then Throw New ArgumentNullException(NameOf(indexOfConnectome))
        If network Is Nothing Then Throw New ArgumentNullException(NameOf(network))

        m_index = indexOfConnectome
        m_layer = network.Network.SparseLayer

        If m_layer Is Nothing Then
            Throw New InvalidOperationException("网络没有装配稀疏递归层，无法用于闭环控制")
        End If

        Units = network.Units
        m_external = New tf.Tensor(1, Units)

        Dim afferents As Integer() = indexOfConnectome.NeuronsOfFlow(ConnectomeIndex.NeuronFlow.Afferent)
        Dim efferents As Integer() = indexOfConnectome.NeuronsOfFlow(ConnectomeIndex.NeuronFlow.Efferent)

        If afferents.Length = 0 OrElse efferents.Length = 0 Then
            Throw New InvalidOperationException(
                "连接组缺少 flow 注释（afferent / efferent），无法建立感觉—运动通路")
        End If

        FlowSummary = $"afferent(感觉输入)={afferents.Length:N0}, efferent(运动输出)={efferents.Length:N0}"

        SensorNeurons = splitChannels(afferents, FlyChannels.ChannelCount, sensorsPerChannel, seed)
        MotorGroups = splitGroups(efferents, groupCount)

        Dim featureCount As Integer = 0

        For Each group As Integer() In MotorGroups
            featureCount += group.Length
        Next

        If featureCount <= 0 Then
            Throw New InvalidOperationException("运动神经元数为 0，无法构造读出特征")
        End If

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
    ''' <param name="sensorValues"><see cref="FlyChannels.ChannelCount"/> 个感觉通道的强度 [0,1]</param>
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

        ' 2) 网络走一步
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
        Dim motorActive As Integer = 0

        For g As Integer = 0 To MotorGroups.Length - 1
            Dim count As Double = 0

            For Each neuron As Integer In MotorGroups(g)
                If spikes(neuron) <> 0 Then
                    count += 1
                    motorActive += 1
                End If
            Next

            m_groupSpikes(g) = count
        Next

        MotorActiveCount = motorActive

        ' 逐运动神经元的滑动窗放电率（回归器特征）
        Dim hist As List(Of tf.Tensor) = m_layer.SHistory
        Dim window As Integer = Math.Max(1, FeatureWindow)
        Dim fromFrame As Integer = Math.Max(0, hist.Count - window)
        Dim frames As Integer = Math.Max(1, hist.Count - fromFrame)
        Dim position As Integer = 0

        For g As Integer = 0 To MotorGroups.Length - 1
            For Each neuron As Integer In MotorGroups(g)
                Dim total As Double = 0

                For t As Integer = fromFrame To hist.Count - 1
                    If hist(t).Data(neuron) <> 0 Then total += 1
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

#Region "停机"

    ''' <summary>
    ''' 释放这份大脑占用的显存：常驻的膜电位 / 脉冲缓冲 + 外部电流张量。
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        If m_disposed Then Return

        m_disposed = True

        If m_layer IsNot Nothing Then
            Try
                Call m_layer.ReleaseDeviceBuffers()
            Catch ex As Exception
                System.Diagnostics.Debug.WriteLine($"unable to release the resident buffers: {ex.Message}")
            End Try
        End If

        If m_external IsNot Nothing Then
            Try
                Call m_external.Dispose()
            Catch
            End Try
        End If
    End Sub

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
            Dim fromIdx As Integer = g * size
            Dim count As Integer = Math.Min(size, pool.Length - fromIdx)

            If count <= 0 Then
                result(g) = New Integer() {}
                Continue For
            End If

            Dim block As Integer() = New Integer(count - 1) {}

            For k As Integer = 0 To count - 1
                block(k) = pool(fromIdx + k)
            Next

            result(g) = block
        Next

        Return result
    End Function

#End Region

End Class
