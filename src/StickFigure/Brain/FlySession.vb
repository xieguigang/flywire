Imports System.Math
Imports std = System.Math

''' <summary>
''' 果蝇大脑 ↔ 火柴人 的闭环会话：感觉编码 → 大脑推进 → 回归读出 → 关节偏置下发。
''' </summary>
''' <remarks>
''' <para>
''' 对应贪吃蛇 demo 里的 <c>FlywireSnake.SnakeSession</c>，但驱动对象是
''' <see cref="FigureEnvironment"/>（3D 物理火柴人），动作也不是离散方向而是
''' <b>连续的 16 维关节角度偏置</b>（叠加在基准 Walk 步态之上）。
''' </para>
''' <para>
''' <b>降频驱动</b>：全脑 13.9 万神经元每次 <c>ForwardStep</c> 都有可观开销，
''' 而物理跑在 60 Hz。所以大脑只在每 <see cref="TickDivisor"/> 个物理步里推进一次
''' （默认 4 → 15 Hz），两次大脑 tick 之间保持上一次的偏置输出。
''' </para>
''' <para>
''' <b>接入方式</b>：调用 <see cref="Install"/> 把内置的
''' <see cref="FlyStickmanAgent"/> 装到 <see cref="FigureEnvironment.Agent"/> 上，
''' 之后 <see cref="FigureEnvironment.Step"/> 的既有循环就会自动调用本类——
''' 物理主循环一行都不用改。
''' </para>
''' </remarks>
Public Class FlySession
    Implements IDisposable

    Private ReadOnly m_env As FigureEnvironment
    Private ReadOnly m_host As FlyBrainHost
    Private ReadOnly m_brain As FlyBrain
    Private ReadOnly m_regressor As FlyRegressor
    Private ReadOnly m_filter As New FlyDecisionFilter()
    Private ReadOnly m_teacher As FlyTeacher
    Private ReadOnly m_agent As FlyStickmanAgent

    Private ReadOnly m_samples As New List(Of FlySample)()

    Private m_stepCounter As Integer
    Private m_brainTicks As Integer
    Private m_disposed As Boolean

    ''' <summary>最近一帧的感觉通道。</summary>
    Public Property LastSensor As FlySensorFrame

    ''' <summary>最近一次大脑 tick 下发的关节偏置（弧度，已滤波）。</summary>
    Public Property LastOffsets As Double() = New Double() {}

    ''' <summary>最近一次教师输出。</summary>
    Public Property LastTeacher As FlyTeacherResult?

    ''' <summary>最近一次大脑 tick 的速度偏置（m/s）。</summary>
    Public Property LastSpeedBias As Double = 0.0

    ''' <summary>最近一次大脑 tick 的转向偏置（[-1,1]）。</summary>
    Public Property LastTurnBias As Double = 0.0

    ''' <summary>大脑已推进的 tick 数。</summary>
    Public ReadOnly Property BrainTicks As Integer
        Get
            Return m_brainTicks
        End Get
    End Property

    ''' <summary>收集到的训练样本数。</summary>
    Public ReadOnly Property SampleCount As Integer
        Get
            Return m_samples.Count
        End Get
    End Property

    ''' <summary>是否正在采集教师标注（DAgger 用）。</summary>
    Public Property Collecting As Boolean = False

    ''' <summary>训练 / 评估的日志回调。</summary>
    Public Property Reporter As Action(Of String)

    Public Sub New(env As FigureEnvironment, host As FlyBrainHost)
        If env Is Nothing Then Throw New ArgumentNullException(NameOf(env))
        If host Is Nothing Then Throw New ArgumentNullException(NameOf(host))

        m_env = env
        m_host = host
        m_brain = host.CreateBrain()
        m_regressor = New FlyRegressor(m_brain.MotorFeatures.Length, 16)
        m_teacher = New FlyTeacher(env.Gait)
        m_agent = New FlyStickmanAgent(Me)

        WeightsFile = IO.Path.Combine(AppContext.BaseDirectory, "fly-brain-readout.csv")
    End Sub

    ''' <summary>果蝇大脑。</summary>
    Public ReadOnly Property Brain As FlyBrain
        Get
            Return m_brain
        End Get
    End Property

    ''' <summary>被驱动的仿真环境。</summary>
    Public ReadOnly Property Environment As FigureEnvironment
        Get
            Return m_env
        End Get
    End Property

    ''' <summary>线性回归读出层。</summary>
    Public ReadOnly Property Regressor As FlyRegressor
        Get
            Return m_regressor
        End Get
    End Property

    ''' <summary>规则教师。</summary>
    Public ReadOnly Property Teacher As FlyTeacher
        Get
            Return m_teacher
        End Get
    End Property

    ''' <summary>输出滤波器。</summary>
    Public ReadOnly Property Filter As FlyDecisionFilter
        Get
            Return m_filter
        End Get
    End Property

    ''' <summary>每多少个物理步推进一次大脑（默认 4 → 15 Hz）。</summary>
    Public Property TickDivisor As Integer = 4

    ''' <summary>读出层权重的存读路径。</summary>
    Public Property WeightsFile As String

    ''' <summary>
    ''' 把大脑装到环境上（<see cref="FigureEnvironment.Agent"/>），开始接管。
    ''' </summary>
    Public Sub Install()
        m_env.Agent = m_agent
        Call m_brain.ResetEpisode()
        Call m_filter.Reset()
        m_stepCounter = 0
    End Sub

    ''' <summary>卸载大脑，交还给脚本 / 演示驱动。</summary>
    Public Sub Uninstall()
        If m_env.Agent Is m_agent Then
            m_env.Agent = New NullAgent()
        End If

        Call m_env.SetAction(Nothing)
    End Sub

    ''' <summary>角色复位时调用（膜电位 / 滤波状态清零）。</summary>
    Public Sub ResetEpisode()
        Call m_brain.ResetEpisode()
        Call m_filter.Reset()
        m_stepCounter = 0
        LastOffsets = New Double() {}
        LastTeacher = Nothing
        LastSpeedBias = 0.0
        LastTurnBias = 0.0
    End Sub

    ''' <summary>清空训练样本。</summary>
    Public Sub ClearSamples()
        Call m_samples.Clear()
    End Sub

#Region "闭环主循环"

    ''' <summary>
    ''' 每个物理步由 <see cref="FlyStickmanAgent"/> 调用：必要时推进大脑，并刷新偏置。
    ''' </summary>
    Public Sub OnPhysicsStep(dt As Double)
        m_stepCounter += 1

        If m_stepCounter >= std.Max(1, TickDivisor) Then
            m_stepCounter = 0
            Call BrainTick(dt * std.Max(1, TickDivisor))
        End If

        ' 速度 / 转向偏置：每个物理步都要下发（它们是步态参数，不是关节）
        Call ApplyDriveBias(dt)
    End Sub

    Private Sub BrainTick(dt As Double)
        ' 1) 感觉编码
        Dim frame As FlySensorFrame = FlySensorEncoder.Encode(m_env)
        LastSensor = frame

        ' 2) 大脑走一步
        Call m_brain.Advance(frame.Values)
        m_brainTicks += 1

        ' 3) 教师信号（拿到本 tick 特征之后产生，保证 (特征, 目标) 配对）
        Dim teacher As FlyTeacherResult = m_teacher.Target(m_env, frame, m_env.Gait, dt)
        LastTeacher = teacher

        ' 4) 回归读出 + 滤波
        Dim raw As Double() = m_regressor.Predict(m_brain.MotorFeatures)
        Dim held As Double() = m_filter.Filter(raw, dt)

        LastOffsets = held
        LastSpeedBias = Clamp(teacher.SpeedBias, -1.5, 1.5)
        LastTurnBias = Clamp(teacher.TurnBias, -1.0, 1.0)

        ' 5) 采集训练样本（模仿学习 / DAgger）
        If Collecting Then
            Call m_samples.Add(New FlySample With {
                .Features = CType(m_brain.MotorFeatures.Clone(), Double()),
                .Offsets = teacher.Offsets,
                .Episode = 0
            })
        End If

        ' 6) 跨栏步：教师要求起跳时给一次冲量
        If teacher.HopSpeed > 0 Then
            m_env.Pose.JumpSpeed = teacher.HopSpeed
        End If
    End Sub

    ''' <summary>
    ''' 把速度 / 转向偏置写进步态参数。
    ''' </summary>
    ''' <remarks>
    ''' 这两个量不是关节而是步态引擎参数：大脑通过它们调节"走多快、往哪偏"，
    ''' 关节偏置则负责具体的肢体动作。
    ''' </remarks>
    Private Sub ApplyDriveBias(dt As Double)
        If LastOffsets Is Nothing OrElse LastOffsets.Length = 0 Then
            Return
        End If

        Dim baseSpeed As Double = std.Max(0.0, m_env.Gait.WalkSpeed)

        m_env.Pose.TargetSpeed = Clamp(baseSpeed + LastSpeedBias, 0.0, 2.5)
        m_env.Gait.Heading += LastTurnBias * 1.5 * If(dt > 0, dt, 0.016)
    End Sub

    Private Shared Function Clamp(v As Double, lo As Double, hi As Double) As Double
        If Double.IsNaN(v) OrElse Double.IsInfinity(v) Then
            Return 0.0
        End If

        Return std.Min(hi, std.Max(lo, v))
    End Function

#End Region

#Region "训练 / 评估"

    ''' <summary>
    ''' 装载读出层权重；文件不存在或维数不符时返回 False。
    ''' </summary>
    Public Function TryLoadWeights(Optional path As String = Nothing) As Boolean
        Dim weightsPath As String = If(path, WeightsFile)

        If Not IO.File.Exists(weightsPath) Then
            Return False
        End If

        Dim loaded As FlyRegressor = FlyRegressor.Load(weightsPath)

        If loaded.Dimension <> m_brain.MotorFeatures.Length OrElse loaded.OutputCount <> 16 Then
            Call LogMessage($"weights dimension mismatch: {loaded.Dimension}x{loaded.OutputCount}, " &
                        $"expect {m_brain.MotorFeatures.Length}x16")
            Return False
        End If

        Call m_regressor.CopyFrom(loaded)

        Return True
    End Function

    ''' <summary>保存读出层权重。</summary>
    Public Sub SaveWeights(Optional path As String = Nothing)
        Call m_regressor.Save(If(path, WeightsFile))
    End Sub

    ''' <summary>
    ''' 模仿学习：采集教师标注 → 训练读出层 → DAgger 修正。
    ''' </summary>
    Public Function Train(episodes As Integer,
                          ticksPerEpisode As Integer,
                          Optional daggerRounds As Integer = 2) As String
        Dim log As New List(Of String)()
        Dim dt As Double = 1.0 / 60.0

        Call m_samples.Clear()

        For round As Integer = 0 To std.Max(0, daggerRounds)
            Call Install()
            Collecting = True

            Dim prior As Integer = m_samples.Count

            For ep As Integer = 1 To std.Max(1, episodes)
                Call m_env.Reset()
                Call ResetEpisode()

                For t As Integer = 1 To ticksPerEpisode
                    Call m_env.Step(dt)
                Next

                Call LogMessage($"  dagger#{round} episode#{ep}: samples={m_samples.Count - prior}, " &
                            $"x={m_env.Position.X:F2}, fallen={m_env.IsFallen}")
                prior = m_samples.Count
            Next

            Collecting = False

            Dim trainError As Double = m_regressor.Train(m_samples, epochs:=8, rate:=0.02, l2:=0.0001)

            Call LogMessage($"  dagger#{round}: trained on {m_samples.Count} samples, " &
                        $"MAE={trainError:F4} rad, {m_regressor.Describe()}")
            Call log.Add($"dagger#{round}: {m_samples.Count} samples, MAE={trainError:F4} rad")
        Next

        Call SaveWeights()
        Call LogMessage($"weights saved: {WeightsFile}")

        Return String.Join(System.Environment.NewLine, log)
    End Function

    ''' <summary>闭环评估：大脑驱动跑一段，返回 (位移, 跌倒帧数)。</summary>
    Public Function Evaluate(ticks As Integer) As (Travelled As Double, Falls As Integer)
        Dim start As Vec3 = m_env.Position
        Dim falls As Integer = 0
        Dim dt As Double = 1.0 / 60.0

        Call Install()

        For i As Integer = 1 To ticks
            Call m_env.Step(dt)

            If m_env.IsFallen Then
                falls += 1
            End If
        Next

        Return (Vec3.Distance(start, m_env.Position), falls)
    End Function

    Private Sub LogMessage(message As String)
        Dim callback As Action(Of String) = Reporter

        If callback IsNot Nothing Then
            Call callback.Invoke(message)
        End If
    End Sub

#End Region

#Region "停机"

    Public Sub Dispose() Implements IDisposable.Dispose
        If m_disposed Then Return

        m_disposed = True

        Call Uninstall()

        If m_brain IsNot Nothing Then
            Call m_brain.Dispose()
        End If
    End Sub

#End Region

#Region "内置 Agent"

    ''' <summary>
    ''' 把 <see cref="FlySession"/> 包装成 <see cref="IStickmanAgent"/>，
    ''' 这样 <see cref="FigureEnvironment.Step"/> 的既有循环无需任何改动。
    ''' </summary>
    Private Class FlyStickmanAgent : Implements IStickmanAgent

        Private ReadOnly m_session As FlySession

        Public Sub New(session As FlySession)
            m_session = session
        End Sub

        Public ReadOnly Property Name As String Implements IStickmanAgent.Name
            Get
                Return $"fly brain (ticks={m_session.BrainTicks})"
            End Get
        End Property

        ''' <summary>连续控制：本类不走离散动作通路，恒返回 -1。</summary>
        Public Function SelectAction(obs As Observation) As Integer Implements IStickmanAgent.SelectAction
            Call m_session.OnPhysicsStep(1.0 / 60.0)

            Return -1
        End Function

        ''' <summary>返回大脑当前下发的关节偏置。</summary>
        Public Function Act(obs As Observation) As Single() Implements IStickmanAgent.Act
            Dim offsets As Double() = m_session.LastOffsets

            If offsets Is Nothing OrElse offsets.Length = 0 Then
                Return Nothing
            End If

            Dim result As Single() = New Single(offsets.Length - 1) {}

            For i As Integer = 0 To offsets.Length - 1
                result(i) = CSng(offsets(i))
            Next

            Return result
        End Function

    End Class

#End Region

End Class
