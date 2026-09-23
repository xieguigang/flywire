Imports System.Text

Namespace Simulation

    ''' <summary>
    ''' 一次电刺激仿真的完整结果（可回放）。
    ''' </summary>
    ''' <remarks>
    ''' 回放需要的是「每个时间步哪些神经元发放了」，而 <c>BrainSimulationResult</c> 只给出
    ''' 逐步的<b>计数</b>（<c>PerStepActive</c>）。因此这里额外保存每一步的激活神经元索引
    ''' （由 <c>SparseLIFLayer.SHistory</c> 的逐步脉冲张量扫描而来），
    ''' 以及整段仿真的逐神经元累计计数（可直接当作"仿真活跃度"着色数据使用）。
    ''' 
    ''' 逐步索引的内存量级很小：全脑 T=30 时每步约数千个神经元，总计约数百 KB。
    ''' </remarks>
    Public Class StimulationReplay

        ''' <summary>刺激方案的描述文本（记录"这一次刺激到底是什么"）。</summary>
        Public Property Label As String = ""

        ''' <summary>被刺激的神经元索引。</summary>
        Public Property Neuron As Integer = -1

        ''' <summary>被刺激神经元的 root_id。</summary>
        Public Property RootId As Long

        ''' <summary>被募集的神经元数量（电极附近一起被兴奋的那一群）。</summary>
        Public Property RecruitedNeurons As Integer

        ''' <summary>募集半径 (纳米)。</summary>
        Public Property RadiusNm As Double

        ''' <summary>注入的刺激强度 (电流，与膜电位阈值同量纲)。</summary>
        Public Property Strength As Double

        ''' <summary>刺激的按住时长 (毫秒)，用于界面回显"这次的强度是怎么来的"。</summary>
        Public Property HoldMilliseconds As Long

        ''' <summary>时间步数 T。</summary>
        Public Property Steps As Integer

        ''' <summary>神经元总数 N。</summary>
        Public Property Units As Integer

        ''' <summary>每一步被激活的神经元索引（长度 = <see cref="Steps"/>）。</summary>
        Public Property ActiveSteps As Integer()()

        ''' <summary>每个时间步的脉冲数（长度 = T）。</summary>
        Public Property SpikesPerStep As Double()

        ''' <summary>每个时间步的活跃神经元数（长度 = T）。</summary>
        Public Property ActivePerStep As Integer()

        ''' <summary>整段仿真的逐神经元累计脉冲计数（长度 = N）。</summary>
        Public Property Counts As Double()

        ''' <summary>整段仿真的脉冲总数。</summary>
        Public Property TotalSpikes As Double

        ''' <summary>
        ''' 有响应（至少发放过一次）的神经元索引，与 <see cref="ResponsePotential"/> 行一一对应。
        ''' </summary>
        ''' <remarks>
        ''' 只保存"有响应"的神经元：全脑 139,255 个神经元里绝大多数全程静止，
        ''' 把它们也存下来会让响应矩阵从几 MB 涨到几十 MB，而绘图时又只会画有响应的那些。
        ''' </remarks>
        Public Property ResponseNeurons As Integer() = New Integer() {}

        ''' <summary>
        ''' 逐神经元的<b>响应电信号强度</b>轨迹：<c>ResponsePotential(k)(t)</c> 是
        ''' 第 <c>ResponseNeurons(k)</c> 个神经元在第 t 步的触发前膜电位。
        ''' </summary>
        ''' <remarks>
        ''' <b>为什么是膜电位而不是脉冲</b>：脉冲是 0/1，画成曲线就是一堆方波，
        ''' "响应强度"根本看不出来；膜电位是连续量，既有亚阈值的积分爬升，
        ''' 也有越阈后的复位跌落，是这一层神经元真实传递的电信号。
        ''' 
        ''' 数据来源是<b>分析回放</b>（同一刺激、逐算子路径重跑一遍）：融合单步内核内部
        ''' 不落膜电位（<c>SparseLIFLayer.UHistory</c> 只在逐算子路径下产生），
        ''' 而两条路径在双精度档下数值等价，因此这次回放给出的就是快速仿真内部的那条轨迹。
        ''' 详见 <c>BrainStimulator.analyzePotential</c>。
        ''' </remarks>
        Public Property ResponsePotential As Double()() = New Double()() {}

        ''' <summary>响应信号的名字（用于图表标题与报告）。</summary>
        Public Property ResponseSignal As String = ""

        ''' <summary>分析回放耗时 (毫秒)。</summary>
        Public Property AnalysisMs As Long

        ''' <summary>
        ''' 分析回放的脉冲序列是否与快速仿真逐位一致（``Nothing`` 表示没做分析回放）。
        ''' </summary>
        ''' <remarks>
        ''' 这是膜电位可信度的<b>自校验</b>：两条路径的脉冲序列一致，才说明分析回放
        ''' 复现的确实是这次刺激实验的动力学；不一致时界面会明确提示不要采信膜电位曲线。
        ''' </remarks>
        Public Property AnalysisMatches As Boolean? = Nothing

        ''' <summary>分析回放中与快速仿真不一致的脉冲数（0 = 完全一致）。</summary>
        Public Property AnalysisMismatches As Integer = 0

        ''' <summary>按神经元索引取响应曲线；没有响应时返回空数组。</summary>
        Public Function ResponseCurve(neuron As Integer) As Double()
            For i As Integer = 0 To ResponseNeurons.Length - 1
                If ResponseNeurons(i) = neuron Then
                    Return ResponsePotential(i)
                End If
            Next

            Return New Double() {}
        End Function

        ''' <summary>神经元索引 → 响应矩阵行号（无响应时为 -1）。</summary>
        Public Function ResponseRow(neuron As Integer) As Integer
            For i As Integer = 0 To ResponseNeurons.Length - 1
                If ResponseNeurons(i) = neuron Then Return i
            Next

            Return -1
        End Function

        ''' <summary>生效的全局权重增益。</summary>
        Public Property Gain As Double

        ''' <summary>实际使用的后端（CUDA / SIMD）。</summary>
        Public Property Backend As String = ""

        ''' <summary>实测走的单步实现路径（Fused / OpByOp）。</summary>
        Public Property StepPath As String = ""

        ''' <summary>回退到逐算子路径的步数（0 表示整段都走在融合路径上）。</summary>
        Public Property FallbackSteps As Integer

        ''' <summary>仿真步循环耗时 (毫秒)。</summary>
        Public Property ElapsedMs As Long

        ''' <summary>从"点击"到"可以回放"的墙钟耗时 (毫秒，含装配与回读)。</summary>
        Public Property WallMs As Long

        ''' <summary>本次结果的落盘目录（``Nothing`` 表示没有落盘）。</summary>
        Public Property ReportDir As String = Nothing

        ''' <summary>落盘的文件清单。</summary>
        Public Property ReportFiles As String() = New String() {}

        ''' <summary>产生结果的时刻。</summary>
        Public Property GeneratedAt As DateTime = DateTime.Now

        ''' <summary>整段仿真中出现过激活的神经元总数（并集）。</summary>
        Public ReadOnly Property ActiveUnionCount As Integer
            Get
                If Counts Is Nothing Then Return 0

                Dim total As Integer = 0

                For i As Integer = 0 To Counts.Length - 1
                    If Counts(i) > 0 Then total += 1
                Next

                Return total
            End Get
        End Property

        ''' <summary>最活跃的那个时间步（回放默认停在这一帧）。</summary>
        Public ReadOnly Property PeakStep As Integer
            Get
                Dim best As Integer = 0
                Dim bestValue As Integer = -1

                If ActivePerStep Is Nothing Then Return 0

                For t As Integer = 0 To ActivePerStep.Length - 1
                    If ActivePerStep(t) > bestValue Then
                        bestValue = ActivePerStep(t)
                        best = t
                    End If
                Next

                Return best
            End Get
        End Property

        ''' <summary>取某一步的激活神经元索引；越界时返回空集。</summary>
        Public Function StepActive(stepIndex As Integer) As Integer()
            If ActiveSteps Is Nothing OrElse stepIndex < 0 OrElse stepIndex >= ActiveSteps.Length Then
                Return New Integer() {}
            End If

            Return ActiveSteps(stepIndex)
        End Function

        ''' <summary>
        ''' 把"第 <paramref name="stepIndex"/> 步及其余辉"折算成逐神经元的显示权重。
        ''' </summary>
        ''' <param name="stepIndex">当前回放到的步号</param>
        ''' <param name="trail">余辉长度（之前多少步仍然可见）</param>
        ''' <returns>
        ''' 长度 = N 的权重数组：当前步为 1.0，余辉按步数线性衰减到 0，
        ''' 未激活的神经元为 0。用于决定点亮颜色与放大倍数。
        ''' </returns>
        ''' <remarks>
        ''' 用权重而不是布尔值，是为了让回放看起来像"一波动起来"：
        ''' 只有当前步的神经元全亮，之前几步的神经元逐渐变暗，
        ''' 否则 T=30 步的轨迹叠在一起会糊成一片。
        ''' </remarks>
        Public Function ActivityMask(stepIndex As Integer, trail As Integer) As Double()
            Dim mask As Double() = New Double(Units - 1) {}

            If trail < 0 Then trail = 0

            For offset As Integer = 0 To trail
                Dim t As Integer = stepIndex - offset

                If t < 0 Then Exit For

                Dim weight As Double = 1.0 - offset / CDbl(trail + 1)
                Dim active As Integer() = StepActive(t)

                For i As Integer = 0 To active.Length - 1
                    Dim neuron As Integer = active(i)

                    If neuron >= 0 AndAlso neuron < mask.Length AndAlso weight > mask(neuron) Then
                        mask(neuron) = weight
                    End If
                Next
            Next

            Return mask
        End Function

        Public Function Describe() As String
            Dim sb As New StringBuilder()

            Call sb.Append($"neuron #{Neuron} (root_id {RootId}) @ {Strength:F2} x {RecruitedNeurons} neurons " &
                           $"(r={RadiusNm / 1000.0:F0} um), T={Steps}, ")
            Call sb.Append($"spikes={TotalSpikes:N0}, active={ActiveUnionCount}/{Units} ({ActiveFraction:P2}), ")
            Call sb.Append($"peak step={PeakStep + 1} ({PeakActive:N0} neurons)")

            If FallbackSteps > 0 Then
                Call sb.Append($", fallback steps={FallbackSteps}")
            End If

            Call sb.Append($", backend={Backend}")

            If AnalysisMatches.HasValue Then
                Call sb.Append($", potential={ResponseNeurons.Length:N0} neurons in {AnalysisMs} ms")

                If Not AnalysisMatches.Value Then
                    Call sb.Append($" (spike mismatches={AnalysisMismatches}, 仅供参考)")
                End If
            End If

            Return sb.ToString
        End Function

        ''' <summary>整段仿真中"至少激活过一次"的神经元占全脑的比例。</summary>
        Public ReadOnly Property ActiveFraction As Double
            Get
                If Units <= 0 Then Return 0

                Return ActiveUnionCount / CDbl(Units)
            End Get
        End Property

        ''' <summary>最活跃步的活跃神经元数。</summary>
        Public ReadOnly Property PeakActive As Integer
            Get
                If ActivePerStep Is Nothing OrElse ActivePerStep.Length = 0 Then Return 0

                Return ActivePerStep(PeakStep)
            End Get
        End Property

    End Class

End Namespace
