Imports System.Drawing
Imports Snake2


    ''' <summary>一个 tick 的完整结果（供界面实时显示）。</summary>
    Public Class SnakeStep

        ''' <summary>本 tick 的 16 个感觉通道强度。</summary>
        Public Property Sensors As Double() = New Double() {}

        ''' <summary>果蝇大脑本 tick 发放的神经元（供三维可视化）。</summary>
        Public Property ActiveNeurons As Integer() = New Integer() {}

        ''' <summary>各运动读出组的本 tick 脉冲数。</summary>
        Public Property MotorSpikes As Double() = New Double() {}

        ''' <summary>解码出的动作编号（0=上 1=下 2=左 3=右）。</summary>
        Public Property Action As Integer

        ''' <summary>该动作对应的绝对方向。</summary>
        Public Property Direction As Point

        ''' <summary>本 tick 之后是否死亡。</summary>
        Public Property Died As Boolean

        ''' <summary>本 tick 之后的得分。</summary>
        Public Property Score As Integer

        ''' <summary>距离最近食物的曼哈顿距离（-1 = 视野里没有食物）。</summary>
        Public Property FoodDistance As Integer

        ''' <summary>教师（示范）动作，仅用于训练数据采集时的诊断。</summary>
        Public Property TeacherAction As Integer = -1

    End Class

    ''' <summary>一局的统计。</summary>
    Public Class SnakeEpisodeResult
        Public Property Steps As Integer
        Public Property Score As Integer
        Public Property Eaten As Integer
        Public Property Died As Boolean
        Public Property Seed As Integer
    End Class

    ''' <summary>
    ''' 果蝇大脑驾驶贪吃蛇的一次"会话"：把游戏、大脑与解码器绑在一起，逐 tick 推进。
    ''' </summary>
    ''' <remarks>
    ''' <b>一个 tick 做什么</b>（与游戏自带的 <c>GameForm.GameTimer_Tick</c> 一一对应）：
    ''' <code>
    '''   1. 读游戏状态 → 编码成 16 个感觉通道强度
    '''   2. 感觉电流注入 afferent 神经元 → 果蝇大脑走一步
    '''   3. 读出 efferent 运动神经元放电率 → 解码器给出动作
    '''   4. 设置蛇的朝向 → 移动 → 判定碰撞 → 游戏世界推进（AI 蛇 / 食物 / 相机）
    ''' </code>
    ''' 
    ''' 本类不创建窗口、不绘制画面：它只推进状态，界面（Neuropils 的观战窗口）订阅
    ''' <see cref="Stepped"/> 事件来取画面与读数。
    ''' </remarks>
    Public Class SnakeSession

        Private ReadOnly m_game As Game
        Private ReadOnly m_render As Render
        Private ReadOnly m_brain As SnakeBrain
        Private m_decoder As SnakeDecoder

        ''' <summary>读出层之后的决策平滑器（抑制逐 tick 左右横跳）。</summary>
        Private ReadOnly m_filter As New SnakeDecisionFilter()

        Private m_lastScore As Integer

        ''' <summary>游戏实例（界面用它绘制画面）。</summary>
        Public ReadOnly Property Game As Game
            Get
                Return m_game
            End Get
        End Property

        ''' <summary>游戏的相机 / 绘制器（界面用它画同一套画面）。</summary>
        Public ReadOnly Property Render As Render
            Get
                Return m_render
            End Get
        End Property

        ''' <summary>果蝇大脑。</summary>
        Public ReadOnly Property Brain As SnakeBrain
            Get
                Return m_brain
            End Get
        End Property

        ''' <summary>
        ''' 运动解码器。
        ''' </summary>
        ''' <remarks>
        ''' 可写：训练完成后把新解码器换上，大脑立刻用学到的读出继续玩
        ''' （训练本身在别的线程 / 别的脑实例上跑，不打断当前这一局）。
        ''' </remarks>
        Public Property Decoder As SnakeDecoder
            Get
                Return m_decoder
            End Get
            Set(value As SnakeDecoder)
                If value Is Nothing Then Return

                m_decoder = value
            End Set
        End Property

        ''' <summary>本局的 tick 数。</summary>
        Public Property Steps As Integer

        ''' <summary>已经玩过的局数。</summary>
        Public Property Episodes As Integer

        ''' <summary>累计吃到的食物（按得分增量统计）。</summary>
        Public Property FoodEaten As Integer

        ''' <summary>历史最高分。</summary>
        Public Property BestScore As Integer

        ''' <summary>最近一个 tick 的结果。</summary>
        Public Property LastStep As SnakeStep

        ''' <summary>决策平滑器（调参 / 诊断用）。</summary>
        Public ReadOnly Property DecisionFilter As SnakeDecisionFilter
            Get
                Return m_filter
            End Get
        End Property

        ''' <summary>
        ''' 每个 tick 之后触发（界面重绘 + 三维活动可视化的数据源）。
        ''' </summary>
        ''' <remarks>形参不能叫 <c>step</c>：那是 VB 的保留字（For ... Step）。</remarks>
        Public Event Stepped(session As SnakeSession, frame As SnakeStep)

        Public Sub New(game As Game, render As Render, brain As SnakeBrain, decoder As SnakeDecoder)
            If game Is Nothing Then Throw New ArgumentNullException(NameOf(game))
            If render Is Nothing Then Throw New ArgumentNullException(NameOf(render))
            If brain Is Nothing Then Throw New ArgumentNullException(NameOf(brain))
            If decoder Is Nothing Then Throw New ArgumentNullException(NameOf(decoder))

            m_game = game
            m_render = render
            m_brain = brain
            m_decoder = decoder
        End Sub

        ''' <summary>当前得分。</summary>
        Public ReadOnly Property Score As Integer
            Get
                Return m_game.score
            End Get
        End Property

        ''' <summary>游戏是否已经结束。</summary>
        Public ReadOnly Property GameOver As Boolean
            Get
                Return m_game.gameOver
            End Get
        End Property

        ''' <summary>开新的一局（重置游戏世界与大脑状态）。</summary>
        Public Sub NewEpisode()
            Call m_game.RestartGame()
            Call m_brain.ResetEpisode()
            Call m_filter.Reset()

            m_lastScore = 0
            Steps = 0
            Episodes += 1
        End Sub

        ''' <summary>
        ''' 推进一个 tick。
        ''' </summary>
        ''' <param name="collect">
        ''' 不为空时，把本 tick 的（运动神经元特征 → 教师动作）追加进去（用于训练解码器）。
        ''' </param>
        ''' <param name="forcedAction">
        ''' 大于等于 0 时强制使用该动作（不看解码器输出）：
        ''' 用于跑"教师策略"对照组，以及调试时手工指定方向。
        ''' </param>
        ''' <remarks>
        ''' 训练样本必须在<b>移动之前</b>采集：特征与教师动作都对应同一个游戏状态，
        ''' 否则学到的是"下一步的状态"对"这一步的动作"，训练出的解码器会整体滞后一拍。
        ''' </remarks>
        Public Function Tick(Optional collect As List(Of SnakeSample) = Nothing,
                             Optional forcedAction As Integer = -1) As SnakeStep
            Dim snake As Snake = m_game.playerSnake

            ' ---- 1) 感觉编码 ----
            Dim frame As SnakeSensorFrame = SnakeSensorEncoder.Encode(m_game)

            ' ---- 2) 训练样本：先把当前状态与教师动作配对记下来 ----
            Dim teacher As Integer = SnakeSensorEncoder.TeacherAction(m_game)

            If collect IsNot Nothing Then
                Dim features As Double() = CType(m_brain.MotorFeatures.Clone(), Double())

                Call collect.Add(New SnakeSample With {
                    .Features = features,
                    .Action = teacher,
                    .Episode = Episodes
                })
            End If

            ' ---- 3) 果蝇大脑走一步 ----
            Call m_brain.Advance(frame.Values)

            ' ---- 4) 运动解码（蛇不能反向，因此把反向动作禁掉）----
            Dim action As Integer

            If forcedAction >= 0 Then
                ' 教师（示范）策略：不经过读出层，也不该被决策平滑器影响
                action = forcedAction
            Else
                Dim forbidden As Integer = reverseAction(snake.Direction)
                Dim scores As Double() = m_decoder.Scores(m_brain.MotorFeatures, forbidden)

                action = m_filter.Decide(scores,
                                         SnakeSensorEncoder.actionOf(snake.Direction),
                                         isBlockedAhead(frame.Values, snake.Direction))
            End If

            Dim direction As Point = SnakeSensors.Directions(action)

            If direction <> snake.Direction Then
                snake.Direction = direction
            End If

            ' ---- 5) 与游戏自身主循环完全一致的推进顺序 ----
            Call snake.Move()
            Call m_game.CheckPlayerCollisions()

            If Not m_game.gameOver Then
                Call m_game.GameTick()
            End If

            Call m_render.UpdateCamera()

            ' ---- 6) 统计与事件 ----
            Steps += 1

            Dim current As Integer = m_game.score
            Dim gained As Integer = current - m_lastScore

            If gained > 0 Then
                FoodEaten += gained
            End If

            m_lastScore = current

            If current > BestScore Then BestScore = current

            Dim report As New SnakeStep With {
                .Sensors = frame.Values,
                .ActiveNeurons = m_brain.ActiveNeurons,
                .MotorSpikes = m_brain.MotorGroupSpikes,
                .Action = action,
                .Direction = direction,
                .Died = m_game.gameOver,
                .Score = current,
                .FoodDistance = frame.FoodDistance,
                .TeacherAction = teacher
            }

            LastStep = report

            RaiseEvent Stepped(Me, report)

            Return report
        End Function

        ''' <summary>
        ''' 跑完整的一局（直到死亡或达到 tick 上限），返回本局统计。
        ''' </summary>
        ''' <param name="markTeacher">
        ''' 用教师（贪心）策略推进而不是解码器：作为"操作逻辑的上限"对照组。
        ''' </param>
        Public Function RunEpisode(Optional maxTicks As Integer = 600,
                                   Optional collect As List(Of SnakeSample) = Nothing,
                                   Optional markTeacher As Boolean = False) As SnakeEpisodeResult

            Call NewEpisode()

            Dim start As Integer = FoodEaten
            Dim ate As Integer = 0

            For i As Integer = 1 To maxTicks
                Dim teacher As Integer = If(markTeacher, SnakeSensorEncoder.TeacherAction(m_game), -1)
                Dim report As SnakeStep = Tick(collect, teacher)

                If report.Died Then
                    Return New SnakeEpisodeResult With {
                        .Steps = i,
                        .Score = report.Score,
                        .Eaten = FoodEaten - start,
                        .Died = True,
                        .Seed = Episodes
                    }
                End If
            Next

            Return New SnakeEpisodeResult With {
                .Steps = maxTicks,
                .Score = m_game.score,
                .Eaten = FoodEaten - start,
                .Died = False,
                .Seed = Episodes
            }
        End Function

        ''' <summary>
        ''' 当前朝向是否已经走不通（前 / 后 / 左 / 右里"该方向"的危险通道亮着）。
        ''' </summary>
        ''' <remarks>
        ''' 危险通道 8..11 是<b>绝对方向</b>，顺序与 <see cref="SnakeSensors.Directions"/> 一致；
        ''' 这个判断只用来给决策平滑器留一个安全阀（走不通时立刻改道），
        ''' 用的信息与大脑拿到的是同一组 16 通道，没有额外作弊。
        ''' </remarks>
        Private Shared Function isBlockedAhead(sensorValues As Double(), direction As Point) As Boolean
            If sensorValues Is Nothing Then Return False

            For i As Integer = 0 To SnakeSensors.DangerChannels - 1
                If SnakeSensors.Directions(i) <> direction Then Continue For

                Dim channel As Integer = SnakeSensors.FoodSectors + i

                Return channel < sensorValues.Length AndAlso sensorValues(channel) >= 0.5
            Next

            Return False
        End Function

        ''' <summary>反向动作编号（用于禁止蛇掉头）。</summary>
        Private Shared Function reverseAction(heading As Point) As Integer
            Dim reverse As New Point(-heading.X, -heading.Y)

            For i As Integer = 0 To SnakeSensorEncoder.ActionCount - 1
                If SnakeSensors.Directions(i) = reverse Then Return i
            Next

            Return -1
        End Function

    End Class

