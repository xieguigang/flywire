Imports System.Math
Imports std = System.Math

''' <summary>
''' 供外部模型（神经网络 / 强化学习）使用的一帧观测。
''' </summary>
''' <remarks>
''' 所有空间量都在<strong>躯干局部坐标系</strong>下表达：
''' 原点 = 骨盆质心，Z = 躯干前向（水平投影），Y = 躯干上向，X = 右向。
''' 这样观测与火柴人的朝向、位置无关，便于策略泛化。
'''
''' 视觉通道是可选的（<see cref="FigureEnvironment.GetObservation(Boolean)"/>），
''' 因为离屏取帧有 GPU 开销，纯本体感觉训练时可以关掉。
''' </remarks>
Public Class Observation

    ''' <summary>仿真时间（秒）。</summary>
    Public Property Timestamp As Double

    ''' <summary>各刚体质心相对骨盆、在躯干局部系下的坐标，按 <see cref="BoneIndex"/> 顺序，每 3 个一组。</summary>
    Public Property JointLocal As Double()

    ''' <summary>各刚体质心速度在躯干局部系下的分量，同上排序。</summary>
    Public Property JointVelocity As Double()

    ''' <summary>躯干上向量（世界系，3 个分量）。</summary>
    Public Property TorsoUp As Double()

    ''' <summary>躯干前向量（世界系，水平投影后归一化，3 个分量）。</summary>
    Public Property TorsoForward As Double()

    ''' <summary>各关节当前相对目标姿态的角度误差（弧度），按 <see cref="JointIndex"/> 顺序。</summary>
    Public Property JointAngleError As Double()

    ''' <summary>骨盆相对脚下地面的高度（m）。</summary>
    Public Property PelvisHeight As Double

    ''' <summary>躯干相对竖直方向的倾角（弧度）。</summary>
    Public Property TiltAngle As Double

    ''' <summary>当前朝向与期望朝向的夹角（弧度，带符号）。</summary>
    Public Property HeadingError As Double

    ''' <summary>水平速度大小（m/s）。</summary>
    Public Property Speed As Double

    ''' <summary>双足触地标志：[左, 右]，1 = 接触。</summary>
    Public Property FootContact As Double()

    ''' <summary>头部视角灰度帧（行优先）。未开启视觉时为空数组。</summary>
    Public Property Vision As Byte()

    ''' <summary>视觉帧宽度。</summary>
    Public Property VisionWidth As Integer
    ''' <summary>视觉帧高度。</summary>
    Public Property VisionHeight As Integer

    ''' <summary>是否已经跌倒。</summary>
    Public Property IsFallen As Boolean

    ''' <summary>
    ''' 把除视觉通道外的所有分量拼成一个 float 向量，直接喂给全连接网络。
    ''' </summary>
    Public Function ToVector() As Single()
        Dim list As New List(Of Single)()

        Call AddAll(list, JointLocal)
        Call AddAll(list, JointVelocity)
        Call AddAll(list, TorsoUp)
        Call AddAll(list, TorsoForward)
        Call AddAll(list, JointAngleError)
        Call AddAll(list, FootContact)

        list.Add(CSng(PelvisHeight))
        list.Add(CSng(TiltAngle))
        list.Add(CSng(HeadingError))
        list.Add(CSng(Speed))
        list.Add(If(IsFallen, 1.0F, 0.0F))

        Return list.ToArray()
    End Function

    Private Shared Sub AddAll(list As List(Of Single), values As Double())
        If values Is Nothing Then
            Return
        End If

        For Each v As Double In values
            list.Add(CSng(v))
        Next
    End Sub

    ''' <summary>观测向量（不含视觉）的维度。</summary>
    Public ReadOnly Property VectorDimension As Integer
        Get
            Return ToVector().Length
        End Get
    End Property

    Public Overrides Function ToString() As String
        Return $"t={Timestamp:F2}s  speed={Speed:F2}m/s  tilt={TiltAngle * 180 / std.PI:F1}°  fallen={IsFallen}"
    End Function
End Class

''' <summary>
''' 外部策略模型需要实现的接口。
''' </summary>
Public Interface IStickmanAgent

    ''' <summary>策略的显示名。</summary>
    ReadOnly Property Name As String

    ''' <summary>
    ''' 由观测给出连续动作向量。
    ''' </summary>
    ''' <param name="obs">当前观测。</param>
    ''' <returns>
    ''' 长度不超过 <see cref="StickmanPose.ActionDimension"/> 的动作向量，
    ''' 语义见 <see cref="StickmanPose.FromActionVector"/>。
    ''' </returns>
    Function Act(obs As Observation) As Single()

    ''' <summary>
    ''' 可选的离散动作选择：返回 &lt; 0 表示沿用连续控制。
    ''' </summary>
    Function SelectAction(obs As Observation) As Integer
End Interface

''' <summary>
''' 内置的占位策略：不做任何决策，完全由预设动作脚本驱动。
''' 接入自己的模型时，实现 <see cref="IStickmanAgent"/> 并赋值给
''' <see cref="FigureEnvironment.Agent"/> 即可。
''' </summary>
Public Class NullAgent : Implements IStickmanAgent

    Public ReadOnly Property Name As String Implements IStickmanAgent.Name
        Get
            Return "null (script driven)"
        End Get
    End Property

    Public Function Act(obs As Observation) As Single() Implements IStickmanAgent.Act
        Return Nothing
    End Function

    Public Function SelectAction(obs As Observation) As Integer Implements IStickmanAgent.SelectAction
        Return -1
    End Function
End Class
