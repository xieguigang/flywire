Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D
Imports Neuropils.Simulation

Namespace Rendering

    ''' <summary>
    ''' 把回放中「某一步及其余辉」映射到点云的颜色与尺寸上。
    ''' </summary>
    ''' <remarks>
    ''' <b>为什么不用"重建点云"的方式做动画</b>
    ''' 
    ''' 每帧重建 13 万个 <see cref="PointCloudPoint"/> 会把回放的帧率压到个位数
    ''' （139,255 次结构体构造 + 字符串装配 + 全量重传）。这里只在<b>变化的那一小部分</b>
    ''' 上下手：维护一份点云的可变副本 + 一份未改动的基准副本，
    ''' 每帧先把上一帧点亮的点还原、再点亮当前帧的点，然后把整份数组交给画布。
    ''' 
    ''' 单帧的改动量 = 当前步 + 余辉涉及的神经元数（全脑规模下通常是几千个），
    ''' 其余 13 万个点完全不动。
    ''' 
    ''' 另外：放大靠的是<b>逐点尺寸系数</b>（<see cref="PointCloudPoint.SizeScale"/>），
    ''' 而不是去改全局点大小 —— 后者会让渲染管线判定几何签名变化并重传整份实例缓冲。
    ''' </remarks>
    Public NotInheritable Class ReplayHighlighter

        ''' <summary>当前步的激活点颜色（最亮）。</summary>
        Private Shared ReadOnly CurrentColor As String = "#ffffff"

        ''' <summary>余辉中段的激活点颜色。</summary>
        Private Shared ReadOnly TrailColor As String = "#ffd166"

        ''' <summary>余辉尾部的激活点颜色。</summary>
        Private Shared ReadOnly FadeColor As String = "#e07a3f"

        Private ReadOnly m_base As PointCloudPoint()
        Private ReadOnly m_points As PointCloudPoint()
        Private ReadOnly m_lookup As Integer()

        ''' <summary>
        ''' 点云当前是否走"热力图"配色（颜色来自调色板查找而不是嵌入色）。
        ''' </summary>
        ''' <remarks>
        ''' 两种配色方式决定"点亮"要改哪个字段：
        ''' 嵌入色模式下改 <c>Color</c>；热力图模式下着色器根本不看 <c>Color</c>，
        ''' 只能把热值抬到量程上限（调色板最亮的那一档）。
        ''' 放大则两种模式都可用（只是承载尺寸系数的字段不同，见 SizeScale）。
        ''' </remarks>
        Private ReadOnly m_heatMap As Boolean

        Private m_peakHeat As Double = Double.NaN

        ''' <summary>上一帧被改写过的神经元（下一帧先还原它们）。</summary>
        Private ReadOnly m_touched As New List(Of Integer)()
        Private m_stimulatedNeuron As Integer = -1

        Public Sub New(scene As BrainScene, units As Integer, Optional heatMap As Boolean = False)
            If scene Is Nothing Then Throw New ArgumentNullException(NameOf(scene))

            m_base = scene.Points
            m_points = CType(m_base.Clone(), PointCloudPoint())
            m_lookup = scene.BuildLookup(System.Math.Max(units, 0))
            m_heatMap = heatMap
        End Sub

        ''' <summary>当前供画布使用的点云（含高亮）。</summary>
        Public ReadOnly Property Points As PointCloudPoint()
            Get
                Return m_points
            End Get
        End Property

        ''' <summary>是否高亮过任何点。</summary>
        Public ReadOnly Property HasHighlight As Boolean
            Get
                Return m_touched.Count > 0 OrElse m_stimulatedNeuron >= 0
            End Get
        End Property

        ''' <summary>
        ''' 点亮第 <paramref name="stepIndex"/> 步及其余辉。
        ''' </summary>
        ''' <param name="replay">刺激结果</param>
        ''' <param name="stepIndex">当前步号（0 基）</param>
        ''' <param name="trail">余辉长度（之前多少步仍可见）</param>
        Public Sub Apply(replay As StimulationReplay, stepIndex As Integer, trail As Integer)
            If replay Is Nothing Then Return

            Call restore()
            Call markStimulated(replay.Neuron)

            Dim mask As Double() = replay.ActivityMask(stepIndex, trail)

            For neuron As Integer = 0 To mask.Length - 1
                Dim weight As Double = mask(neuron)

                If weight <= 0 Then Continue For

                Dim index As Integer = neuronPoint(neuron)

                If index < 0 Then Continue For

                m_points(index) = highlight(m_base(index), weight)
                Call m_touched.Add(neuron)
            Next
        End Sub

        ''' <summary>把一个基准点改写成"被点亮"的样子。</summary>
        Private Function highlight(point As PointCloudPoint, weight As Double) As PointCloudPoint
            If m_heatMap Then
                ' 热力图：颜色由调色板按热值查表，因此"点亮"= 把热值抬到当前量程的上限。
                ' 不能再往上抬 —— 归一化区间是按传入的点现算的，抬过头会把其它点全压暗
                point.Intensity = peakHeat()
            Else
                point.Color = colorOf(weight)
            End If

            point.SizeScale = 1.0 + weight * SizeBoost

            Return point
        End Function

        ''' <summary>
        ''' 当前点云折算出来的最大热值（热力图模式下的"最亮档"）。
        ''' </summary>
        ''' <remarks>
        ''' 必须与渲染管线的折算方式<b>完全一致</b>：热值取 0 时管线会用 Z 坐标代替
        ''' （<c>GpuSceneGeometry.BuildCloudInstances</c>），因此最大值要在
        ''' <c>Intensity ≠ 0 ? Intensity : Z</c> 上取。
        ''' 照搬 <c>Intensity</c> 的最大值会算出一个低于实际量程上限的数 ——
        ''' 那样"点亮"的神经元只是调色板中段，看起来并不比原有的高活跃神经元更亮。
        ''' 
        ''' 取到真正的上限值还有个附带好处：归一化区间不受影响（这个值本来就存在于点云里），
        ''' 其余点不会被压暗。
        ''' </remarks>
        Private Function peakHeat() As Double
            If Not Double.IsNaN(m_peakHeat) Then Return m_peakHeat

            Dim peak As Double = 0

            For i As Integer = 0 To m_base.Length - 1
                Dim value As Double = If(m_base(i).Intensity <> 0, m_base(i).Intensity, m_base(i).Z)

                If value > peak Then
                    peak = value
                End If
            Next

            ' 全零（整幅点云都没有活跃度）时用 1.0：归一化会退化成 [0,1]，取上限仍是"最亮"
            If peak <= 0 Then peak = 1.0

            m_peakHeat = peak

            Return peak
        End Function

        ''' <summary>
        ''' 按"逐神经元权重"直接高亮（用于非回放的实时场景，例如果蝇大脑玩游戏时的活动）。
        ''' </summary>
        ''' <param name="mask">长度 = 神经元数 的权重数组：0 = 不亮，1 = 最亮</param>
        ''' <param name="actual">本帧真正被点亮的神经元（用于下一帧还原）</param>
        ''' <remarks>
        ''' 与 <see cref="Apply"/> 共用同一套"先还原上一帧、再点亮当前帧"的机制，
        ''' 因此单帧开销只与变化的那部分神经元数量有关。
        ''' </remarks>
        Public Sub ApplyMask(mask As Double(), actual As Integer())
            If mask Is Nothing Then Return

            Call restore()

            If actual IsNot Nothing Then
                For i As Integer = 0 To actual.Length - 1
                    Dim neuron As Integer = actual(i)
                    Dim index As Integer = neuronPoint(neuron)

                    If index < 0 Then Continue For

                    Dim weight As Double = If(neuron >= 0 AndAlso neuron < mask.Length, mask(neuron), 0.6)

                    m_points(index) = highlight(m_base(index), If(weight <= 0, 0.6, weight))
                    Call m_touched.Add(neuron)
                Next
            Else
                For neuron As Integer = 0 To mask.Length - 1
                    Dim weight As Double = mask(neuron)

                    If weight <= 0 Then Continue For

                    Dim index As Integer = neuronPoint(neuron)

                    If index < 0 Then Continue For

                    m_points(index) = highlight(m_base(index), weight)
                    Call m_touched.Add(neuron)
                Next
            End If
        End Sub

        ''' <summary>还原全部高亮（结束回放 / 关闭刺激模式）。</summary>
        Public Sub Reset()
            Call restore()

            m_stimulatedNeuron = -1
        End Sub

        ''' <summary>只标出"本次被刺激的神经元"（还没开始回放时也能看到电极在哪）。</summary>
        Public Sub MarkStimulus(neuron As Integer)
            Call markStimulated(neuron)
        End Sub

        Private Sub restore()
            For i As Integer = 0 To m_touched.Count - 1
                Dim index As Integer = neuronPoint(m_touched(i))

                If index >= 0 Then
                    m_points(index) = m_base(index)
                End If
            Next

            m_touched.Clear()

            If m_stimulatedNeuron >= 0 Then
                Dim index As Integer = neuronPoint(m_stimulatedNeuron)

                If index >= 0 Then
                    m_points(index) = m_base(index)
                End If

                m_stimulatedNeuron = -1
            End If
        End Sub

        Private Sub markStimulated(neuron As Integer)
            If neuron < 0 Then Return

            Dim index As Integer = neuronPoint(neuron)

            If index < 0 Then Return

            m_points(index) = highlight(m_base(index), 1.5)
            m_stimulatedNeuron = neuron
        End Sub

        Private Function neuronPoint(neuron As Integer) As Integer
            If m_lookup Is Nothing OrElse neuron < 0 OrElse neuron >= m_lookup.Length Then
                Return -1
            End If

            Return m_lookup(neuron)
        End Function

        Private Shared Function colorOf(weight As Double) As String
            If weight >= 0.999 Then Return CurrentColor
            If weight >= 0.6 Then Return TrailColor

            Return FadeColor
        End Function

        ''' <summary>当前步的激活点在全局点大小基础上的放大倍数。</summary>
        Public Const SizeBoost As Double = 2.6

    End Class

End Namespace
