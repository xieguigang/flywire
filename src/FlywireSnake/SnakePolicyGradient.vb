' 注意：这里不再显式写 Namespace FlywireSnake ——
' VB 工程未指定 RootNamespace 时会默认用工程名作为根命名空间，
' 再手写一层就会变成 FlywireSnake.FlywireSnake.*（外部引用时找不到类型）。

    ''' <summary>
    ''' 在"模仿学习 + DAgger"之上再补一层强化学习：<b>直接用游戏得分</b>微调运动读出层。
    ''' </summary>
    ''' <remarks>
    ''' <b>为什么模仿学习不够</b>：读出层与示范动作的逐 tick 一致率能到 74%~78%，
    ''' 但真正上场只有 8 分左右（同一个示范教师是 95 分）。原因是<b>误差累积</b>：
    ''' 一旦读出层走偏，蛇就进入了训练集从没覆盖过的状态，之后只会越偏越远。
    ''' DAgger 能把"自己走到的状态"补进训练集，但仍然是在拟合教师的动作，
    ''' 而教师动作本身在噪声特征上就学不准（一致率上不去了）。
    ''' 
    ''' 这一层换个目标：不再"像教师"，而是"把分数拿回来"。方法是策略梯度
    ''' （REINFORCE + 滑动基线），在读出层自己走出来的轨迹上学。
    ''' 
    ''' <b>reward 怎么给</b>：
    ''' <list type="bullet">
    '''   <item><b>吃到食物</b>：按游戏真实得分增量（常规 1 / 活动 5 / 超级 30）—— 这是最终目标；</item>
    '''   <item><b>靠近猎物</b>：按"折算代价"（距离 − 价值折分）的下降量给密集奖励
    '''         （<see cref="ShapingScale"/>），否则整局只有几次稀疏奖励，梯度噪声会压过信号；</item>
    '''   <item><b>每走一步</b>：小额罚分，鼓励尽快吃到；</item>
    '''   <item><b>撞死</b>：一次性罚分。</item>
    ''' </list>
    ''' 
    ''' <b>安全网</b>：强化学习过程中策略可能一时变差，因此每练完一块就评一次分，
    ''' 只保留分数最高的那一版权重（<see cref="Train"/> 结束时会恢复它）。
    ''' 最坏情况下结果与"不做强化学习"持平。
    ''' </remarks>
    Public NotInheritable Class SnakePolicyGradient

        Private ReadOnly m_decoder As SnakeDecoder
        Private ReadOnly m_rng As Random

        ''' <summary>每局的平均回报（滑动），诊断用。</summary>
        Private m_baseline As Double

        ''' <summary>回报尺度的滑动标准差（用作优势的归一化因子）。</summary>
        Private m_advantageScale As Double = 1.0

        ''' <summary>
        ''' 对数几率(softmax logits)在动作之间的滑动标准差 —— 温度的分母。
        ''' </summary>
        ''' <remarks>
        ''' 读出层的 logits 量级很小（权重 ~1e-2 × 放电率 ~0.25，动作之间的差异只有 1e-2 量级），
        ''' 直接 softmax 会得到一个近似均匀的策略：既没有利用（等于乱选），也没有梯度尺度。
        ''' 因此把它按"动作之间的标准差"归一化：整局都在同一个尺度上比较，
        ''' 但"哪一步更果断"（差异更大）依然会被保留下来。
        ''' </remarks>
        Private m_logitScale As Double

        Private m_hasBaseline As Boolean
        Private m_episodes As Integer

        ''' <summary>学习率（按决策步数平摊后的每步步长）。</summary>
        Public Property LearningRate As Double = 0.001

        ''' <summary>折扣因子。</summary>
        Public Property Discount As Double = 0.97

        ''' <summary>logit 锐化增益：归一化之后"最好 / 最差动作"的概率比大约就是它。</summary>
        Public Property Gain As Double = 1.5

        ''' <summary>密集奖励（靠近猎物）的权重。</summary>
        Public Property ShapingScale As Double = 0.05

        ''' <summary>每步的小额罚分。</summary>
        Public Property StepPenalty As Double = 0.01

        ''' <summary>撞死的一次性罚分。</summary>
        Public Property DeathPenalty As Double = 1.0

        ''' <summary>
        ''' 单个权重的单步变化上限（防止某一局把权重带飞）。
        ''' </summary>
        ''' <remarks>
        ''' 读出层权重的量级只有 1e-2，所以这个上限必须比它小一个量级 ——
        ''' 实测给到 2e-3 时，一局就能把权重改成另一副样子，策略当场崩掉。
        ''' </remarks>
        Public Property MaxWeightStep As Double = 0.0002

        ''' <summary>采样时的锐化系数上限（logit 尺度极小时不至于退化成"只取最大"）。</summary>
        Public Property MaxSharpness As Double = 50.0

        ''' <summary>已练的局数。</summary>
        Public ReadOnly Property Episodes As Integer
            Get
                Return m_episodes
            End Get
        End Property

        Private m_lastReturn As Double

        ''' <summary>最近一局的"每步平均回报"。</summary>
        Public ReadOnly Property LastReturn As Double
            Get
                Return m_lastReturn
            End Get
        End Property

        Public Sub New(decoder As SnakeDecoder, Optional seed As Integer = 20260923)
            If decoder Is Nothing Then Throw New ArgumentNullException(NameOf(decoder))

            m_decoder = decoder
            m_rng = New Random(seed)
        End Sub

#Region "训练"

        ''' <summary>
        ''' 跑若干局策略梯度（<b>就地</b>更新解码器权重），返回评估到的最好分数。
        ''' </summary>
        ''' <param name="session">用来跑游戏的会话（本方法会临时占用它的动作选择器）</param>
        ''' <param name="episodes">总训练局数（会按 <paramref name="blockSize"/> 分块评估）</param>
        ''' <param name="blockSize">每练几局评一次分</param>
        ''' <param name="maxTicks">单局 tick 上限</param>
        ''' <param name="evaluate">给定"当前权重"的闭环评分函数（见 <see cref="SnakePlayground.Evaluate"/>）</param>
        ''' <param name="reporter">进度回调</param>
        ''' <remarks>
        ''' 需要 <paramref name="evaluate"/> 而不是自己去评：只有装配方（<see cref="SnakePlayground"/>）
        ''' 知道该怎么开一局干净的评估会话。
        ''' </remarks>
        Public Function Train(session As SnakeSession,
                              episodes As Integer,
                              blockSize As Integer,
                              maxTicks As Integer,
                              evaluate As Func(Of Double),
                              Optional reporter As Action(Of String) = Nothing) As Double

            If session Is Nothing Then Throw New ArgumentNullException(NameOf(session))

            Dim weights As Double()() = m_decoder.Weights()
            Dim featureTrace As New List(Of Double())
            ' 注意别写成 New List(Of Double)()：那会变成一个"数组"，类型对不上
            Dim actionTrace As New List(Of Integer)
            Dim probabilityTrace As New List(Of Double())
            Dim sharpTrace As New List(Of Double)
            Dim best As Double()() = Snapshot()
            Dim bestScore As Double = If(evaluate Is Nothing, Double.NegativeInfinity, evaluate())

            session.Sampler = Function(features As Double(), forbidden As Integer) As Integer
                                  Return sample(features, forbidden, featureTrace, actionTrace,
                                                probabilityTrace, sharpTrace)
                              End Function

            Try
                Dim total As Integer = Math.Max(1, episodes)
                Dim block As Integer = Math.Max(1, blockSize)

                For start As Integer = 0 To total - 1 Step block
                    Dim finish As Integer = Math.Min(total, start + block)

                    For episode As Integer = start + 1 To finish
                        Call runEpisode(session, maxTicks, featureTrace, actionTrace,
                                        probabilityTrace, sharpTrace)
                    Next

                    m_episodes += (finish - start)

                    If evaluate Is Nothing Then Continue For

                    Dim score As Double = evaluate()

                    If reporter IsNot Nothing Then
                        Call reporter($"reinforce {m_episodes} 局：闭环评分 {score:F2}" &
                                      $"（最好 {Math.Max(score, bestScore):F2}，基线回报 {m_baseline:F3}）")
                    End If

                    ' 只留最好的一版：强化学习中途变差也不会把已有成果丢掉
                    If score > bestScore Then
                        bestScore = score
                        best = Snapshot()
                    End If
                Next
            Finally
                session.Sampler = Nothing
            End Try

            Call Restore(best)

            Return bestScore
        End Function

        ''' <summary>跑一局并把梯度就地写回权重。</summary>
        Private Sub runEpisode(session As SnakeSession,
                               maxTicks As Integer,
                               featureTrace As List(Of Double()),
                               actionTrace As List(Of Integer),
                               probabilityTrace As List(Of Double()),
                               sharpTrace As List(Of Double))

            featureTrace.Clear()
            actionTrace.Clear()
            probabilityTrace.Clear()
            sharpTrace.Clear()

            Call session.NewEpisode()

            Dim rewards As New List(Of Double)
            Dim lastScore As Integer = session.Score
            Dim lastCost As Double = preyCost(session)

            For t As Integer = 1 To Math.Max(1, maxTicks)
                Call session.Tick()

                Dim cost As Double = preyCost(session)
                Dim reward As Double = (session.Score - lastScore) _
                                       + ShapingScale * (lastCost - cost) _
                                       - StepPenalty

                lastScore = session.Score
                lastCost = cost

                If session.GameOver Then reward -= DeathPenalty

                Call rewards.Add(reward)

                If session.GameOver Then Exit For
            Next

            Dim sum As Double = 0

            For i As Integer = 0 To rewards.Count - 1
                sum += rewards(i)
            Next

            m_lastReturn = If(rewards.Count = 0, 0, sum / rewards.Count)

            Call update(featureTrace, actionTrace, probabilityTrace, sharpTrace, rewards)
        End Sub

        ''' <summary>折扣回报 → 优势 → 就地更新权重。</summary>
        Private Sub update(features As List(Of Double()),
                           actions As List(Of Integer),
                           probabilities As List(Of Double()),
                           sharps As List(Of Double),
                           rewards As List(Of Double))

            Dim count As Integer = rewards.Count

            If count = 0 OrElse actions.Count < count Then Return

            ' ---- 回报（从后往前做折扣累加）----
            Dim returns As Double() = New Double(count - 1) {}
            Dim accumulated As Double = 0

            For t As Integer = count - 1 To 0 Step -1
                accumulated = rewards(t) + Discount * accumulated
                returns(t) = accumulated
            Next

            ' ---- 基线（回报的滑动均值）与尺度（滑动标准差）----
            Dim mean As Double = 0

            For t As Integer = 0 To count - 1
                mean += returns(t)
            Next

            mean /= count

            Dim variance As Double = 0

            For t As Integer = 0 To count - 1
                variance += (returns(t) - mean) ^ 2
            Next

            variance = Math.Max(0, variance / count)

            If m_hasBaseline Then
                m_baseline = 0.9 * m_baseline + 0.1 * mean
                m_advantageScale = 0.9 * m_advantageScale + 0.1 * Math.Sqrt(variance)
            Else
                m_baseline = mean
                m_advantageScale = Math.Sqrt(variance)
                m_hasBaseline = True
            End If

            m_advantageScale = Math.Max(m_advantageScale, 1.0E-6)

            ' 把学习率按决策步数平摊：局有长有短，但每一局对权重的总扰动应当差不多
            Dim rate As Double = LearningRate / count
            Dim weights As Double()() = m_decoder.Weights()

            For t As Integer = 0 To count - 1
                Dim advantage As Double = (returns(t) - m_baseline) / m_advantageScale
                Dim feature As Double() = features(t)
                Dim probability As Double() = probabilities(t)
                Dim sharp As Double = sharps(t)
                Dim taken As Integer = actions(t)

                If feature Is Nothing OrElse probability Is Nothing Then Continue For

                For a As Integer = 0 To weights.Length - 1
                    ' 取到的那一路概率要抬高，其余压低：这是 softmax 策略对权重的梯度方向
                    Dim delta As Double = If(a = taken, 1.0 - probability(a), -probability(a))

                    If delta = 0 Then Continue For

                    Dim row As Double() = weights(a)

                    ' 梯度里<b>不</b>乘 sharp：它是个"按尺度归一化"的常数，
                    ' 乘进去就等于把步长放大到不受控的量级（实测直接把策略练崩）。
                    ' sharp 只用于采样时的温度。
                    Dim scale As Double = rate * advantage * delta

                    For i As Integer = 0 To Math.Min(row.Length, feature.Length) - 1
                        Dim value As Double = feature(i)

                        If value = 0 Then Continue For

                        ' 变量不能叫 step：那是 VB 的保留字（For ... Step）
                        Dim change As Double = scale * value

                        If change > MaxWeightStep Then
                            change = MaxWeightStep
                        ElseIf change < -MaxWeightStep Then
                            change = -MaxWeightStep
                        End If

                        row(i) += change
                    Next
                Next
            Next
        End Sub

#End Region

#Region "动作采样"

        ''' <remarks>
        ''' 采样时把 (特征, 动作, 概率, 锐化系数) 记进轨迹 —— 梯度要用它们回放。
        ''' </remarks>
        Private Function sample(features As Double(),
                                forbidden As Integer,
                                featureTrace As List(Of Double()),
                                actionTrace As List(Of Integer),
                                probabilityTrace As List(Of Double()),
                                sharpTrace As List(Of Double)) As Integer

            Dim logits As Double() = m_decoder.Scores(features, forbidden)
            Dim allowed As New List(Of Integer)
            Dim mean As Double = 0

            For a As Integer = 0 To logits.Length - 1
                If Double.IsNegativeInfinity(logits(a)) Then Continue For

                Call allowed.Add(a)

                mean += logits(a)
            Next

            If allowed.Count = 0 Then Return 0

            mean /= allowed.Count

            Dim variance As Double = 0

            For Each a As Integer In allowed
                variance += (logits(a) - mean) ^ 2
            Next

            variance /= allowed.Count

            Dim deviation As Double = Math.Sqrt(variance)

            ' 温度分母：用整局尺度的滑动值（而不是每一步各自归一化），
            ' 这样"某一步特别果断"依然会体现为更尖的分布
            If deviation > 0 Then
                m_logitScale = If(m_logitScale <= 0, deviation, 0.95 * m_logitScale + 0.05 * deviation)
            End If

            Dim sharp As Double = If(m_logitScale <= 0, 0.0, Math.Min(MaxSharpness, Gain / m_logitScale))
            Dim total As Double = 0

            For Each a As Integer In allowed
                total += Math.Exp(sharp * (logits(a) - mean))
            Next

            Dim roll As Double = m_rng.NextDouble() * total
            Dim picked As Integer = allowed(0)
            Dim accumulated As Double = 0

            For Each a As Integer In allowed
                accumulated += Math.Exp(sharp * (logits(a) - mean))

                If roll <= accumulated Then
                    picked = a

                    Exit For
                End If
            Next

            Dim probability As Double() = New Double(logits.Length - 1) {}

            If total > 0 Then
                For Each a As Integer In allowed
                    probability(a) = Math.Exp(sharp * (logits(a) - mean)) / total
                Next
            End If

            Call featureTrace.Add(CType(features.Clone(), Double()))
            Call actionTrace.Add(picked)
            Call probabilityTrace.Add(probability)
            Call sharpTrace.Add(sharp)

            Return picked
        End Function

        ''' <summary>离"最划算的猎物"的折算代价（移动前后的差值就是这一小步的密集奖励）。</summary>
        Private Shared Function preyCost(session As SnakeSession) As Double
            Return SnakeSensorEncoder.PreyCost(session.Game, session.Game.playerSnake.Head)
        End Function

#End Region

#Region "权重快照"

        ''' <summary>当前权重的深拷贝（"只留最好的一版"用）。</summary>
        Public Function Snapshot() As Double()()
            Dim weights As Double()() = m_decoder.Weights()
            Dim copy As Double()() = New Double(weights.Length - 1)() {}

            For a As Integer = 0 To weights.Length - 1
                copy(a) = CType(weights(a).Clone(), Double())
            Next

            Return copy
        End Function

        ''' <summary>把权重恢复成某个快照。</summary>
        Public Sub Restore(snapshot As Double()())
            If snapshot Is Nothing Then Return

            Dim weights As Double()() = m_decoder.Weights()

            For a As Integer = 0 To Math.Min(weights.Length, snapshot.Length) - 1
                Call Array.Copy(snapshot(a), weights(a), Math.Min(snapshot(a).Length, weights(a).Length))
            Next
        End Sub

#End Region

    End Class
