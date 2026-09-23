' 注意：这里不再显式写 Namespace FlywireSnake ——
' VB 工程未指定 RootNamespace 时会默认用工程名作为根命名空间，
' 再手写一层就会变成 FlywireSnake.FlywireSnake.*（外部引用时找不到类型）。

    ''' <summary>
    ''' 读出层之后的决策平滑器：把"逐 tick 各自取最大值"变成"带惯性的一串决策"。
    ''' </summary>
    ''' <remarks>
    ''' <b>为什么需要它</b>：运动神经元的放电率本身带噪声，而且大脑对感觉输入有响应延迟，
    ''' 于是每一 tick 独立取 argmax 会让蛇在相邻 tick 之间左右横跳 ——
    ''' 从观战窗口看就是"没有目标地乱转"（随机游走）。这里做两件事：
    ''' 
    ''' <list type="bullet">
    '''   <item><b>平滑</b>：对各动作的打分做指数滑动平均（顺带把大脑的响应延迟压平）；</item>
    '''   <item><b>迟滞</b>：换方向要有代价 —— 新方向的平滑打分必须比"保持当前方向"
    '''         高出 <see cref="Margin"/> 才会转，否则继续走。</item>
    ''' </list>
    ''' 
    ''' 另外留了一个安全阀：当前方向如果<b>已经走不通</b>（该方向的危险通道亮着），
    ''' 就忽略迟滞立刻重新决策 —— 迟滞不该让蛇径直撞上去。
    ''' </remarks>
    Public NotInheritable Class SnakeDecisionFilter

        ''' <summary>滑动平均系数：越大越相信当前这一 tick 的打分。</summary>
        Public Property Smoothing As Double = 0.45

        ''' <summary>
        ''' 转向迟滞的<b>相对</b>余量：占"当前各动作打分跨度"的比例（0 = 不迟滞，永远取最高分方向）。
        ''' </summary>
        ''' <remarks>
        ''' 必须相对而不能绝对：打分的绝对量级取决于读出层权重与放电率，
        ''' 一个固定的绝对余量要么小到形同虚设，要么大到把蛇钉死在直线上（实测过后者）。
        ''' </remarks>
        Public Property Margin As Double = 0.15

        Private m_smoothed As Double()

        ''' <summary>平滑后的各动作打分（诊断用；还没决策过时为 Nothing）。</summary>
        Public ReadOnly Property Smoothed As Double()
            Get
                Return m_smoothed
            End Get
        End Property

        ''' <summary>开新一局时清空历史：上一局的走向不该带进这一局。</summary>
        Public Sub Reset()
            m_smoothed = Nothing
        End Sub

        ''' <summary>
        ''' 在读出层的原始打分上做平滑与迟滞，给出本 tick 的动作。
        ''' </summary>
        ''' <param name="scores">读出层对各动作的打分（禁止的动作是 <c>-∞</c>）</param>
        ''' <param name="incumbent">当前朝向对应的动作编号（"继续保持"的候选）</param>
        ''' <param name="mustTurn">当前方向是否已经走不通（被挡住 / 是禁止的反向）</param>
        Public Function Decide(scores As Double(), incumbent As Integer, mustTurn As Boolean) As Integer
            If scores Is Nothing OrElse scores.Length = 0 Then Return Math.Max(incumbent, 0)

            Call smooth(scores)

            Dim best As Integer = -1
            Dim bestScore As Double = Double.NegativeInfinity
            Dim worstScore As Double = Double.PositiveInfinity

            For a As Integer = 0 To m_smoothed.Length - 1
                Dim value As Double = m_smoothed(a)

                ' 被禁的方向不参与"谁最好"的比较
                If Double.IsNegativeInfinity(value) Then Continue For

                If value > bestScore Then
                    bestScore = value
                    best = a
                End If

                If value < worstScore Then worstScore = value
            Next

            ' 全部被禁（理论上只有"四个方向都不可行"才会出现）：保持原方向
            If best < 0 Then Return Math.Max(incumbent, 0)
            If mustTurn Then Return best

            If Margin <= 0 OrElse incumbent < 0 OrElse incumbent >= m_smoothed.Length Then Return best

            Dim held As Double = m_smoothed(incumbent)

            If Double.IsNegativeInfinity(held) Then Return best
            If worstScore > bestScore Then worstScore = bestScore

            ' 原方向没被"明显超过"（超过当前打分跨度的 Margin 倍）就继续走：这就是迟滞
            If held >= bestScore - Margin * (bestScore - worstScore) Then Return incumbent

            Return best
        End Function

        ''' <remarks>
        ''' 被禁止的动作（<c>-∞</c>）既不参与平滑，也不能被平滑"记住"：
        ''' 否则蛇转向之后，原本被禁的方向会因为 <c>-∞ × (1 - rate)</c> 的余毒而长时间抬不起头。
        ''' </remarks>
        Private Sub smooth(scores As Double())
            If m_smoothed Is Nothing OrElse m_smoothed.Length <> scores.Length Then
                m_smoothed = New Double(scores.Length - 1) {}

                Call Array.Copy(scores, m_smoothed, scores.Length)

                Return
            End If

            Dim rate As Double = Math.Max(0.0, Math.Min(1.0, Smoothing))

            For a As Integer = 0 To scores.Length - 1
                If Double.IsNegativeInfinity(scores(a)) Then
                    m_smoothed(a) = Double.NegativeInfinity

                    Continue For
                End If

                If Double.IsNegativeInfinity(m_smoothed(a)) Then
                    ' 上一 tick 被禁、这一 tick 解禁：用本 tick 的打分重新起头
                    m_smoothed(a) = scores(a)

                    Continue For
                End If

                m_smoothed(a) = (1.0 - rate) * m_smoothed(a) + rate * scores(a)
            Next
        End Sub

    End Class
