''' <summary>
''' 火柴人一帧的目标姿态：各关节的目标角度 + 根部的驱动指令。
''' </summary>
''' <remarks>
''' 角度单位统一为弧度，符号约定如下（以火柴人自身坐标系为准：+Z 前、+Y 上、+X 右）：
''' <list type="bullet">
''' <item><description>髋/肩 pitch &gt; 0：肢体向<strong>前</strong>摆；</description></item>
''' <item><description>膝/肘 bend &gt; 0：关节<strong>屈曲</strong>（小腿向后、前臂向前收）；</description></item>
''' <item><description>踝 pitch &gt; 0：脚尖向<strong>下</strong>（跖屈）；</description></item>
''' <item><description>躯干 pitch &gt; 0：上身<strong>前倾</strong>，roll &gt; 0 向<strong>左</strong>倾，yaw &gt; 0 向<strong>左</strong>转。</description></item>
''' </list>
''' 该结构同时就是"神经网络连续动作向量"的语义化视图：外部模型输出的浮点向量
''' 按同样的顺序写入这些字段即可驱动火柴人。
''' </remarks>
Public Class StickmanPose

    ' ---------- 下肢 ----------
    ''' <summary>左髋前后摆角（+ 前摆）。</summary>
    Public HipPitchL As Double
    ''' <summary>右髋前后摆角（+ 前摆）。</summary>
    Public HipPitchR As Double
    ''' <summary>左髋外展角。</summary>
    Public HipRollL As Double
    ''' <summary>右髋外展角。</summary>
    Public HipRollR As Double
    ''' <summary>左膝屈曲角。</summary>
    Public KneeL As Double
    ''' <summary>右膝屈曲角。</summary>
    Public KneeR As Double
    ''' <summary>左踝俯仰（在"保持脚掌水平"的基础上叠加）。</summary>
    Public AnkleL As Double
    ''' <summary>右踝俯仰。</summary>
    Public AnkleR As Double

    ' ---------- 上肢 ----------
    ''' <summary>左肩前后摆角（+ 前摆）。</summary>
    Public ShoulderPitchL As Double
    ''' <summary>右肩前后摆角。</summary>
    Public ShoulderPitchR As Double
    ''' <summary>左肩外展角。</summary>
    Public ShoulderRollL As Double
    ''' <summary>右肩外展角。</summary>
    Public ShoulderRollR As Double
    ''' <summary>左肘屈曲角。</summary>
    Public ElbowL As Double
    ''' <summary>右肘屈曲角。</summary>
    Public ElbowR As Double

    ' ---------- 躯干与头颈 ----------
    ''' <summary>腰（脊柱）前倾角。</summary>
    Public SpinePitch As Double
    ''' <summary>腰（脊柱）侧倾角。</summary>
    Public SpineRoll As Double
    ''' <summary>腰（脊柱）扭转角。</summary>
    Public SpineYaw As Double
    ''' <summary>颈部俯仰角（+ 低头）。</summary>
    Public NeckPitch As Double
    ''' <summary>颈部左右转角。</summary>
    Public NeckYaw As Double

    ' ---------- 根部驱动 ----------
    ''' <summary>期望的水平行走速度（m/s，沿 <see cref="TargetHeading"/> 方向）。</summary>
    Public TargetSpeed As Double
    ''' <summary>期望朝向（弧度，0 = +Z）。</summary>
    Public TargetHeading As Double
    ''' <summary>骨盆相对脚下地面的目标高度（m）。</summary>
    Public PelvisHeight As Double = 0.98
    ''' <summary>&gt; 0 时本帧施加一次起跳速度（m/s）。</summary>
    Public JumpSpeed As Double = 0.0

    ''' <summary>重置为直立站姿。</summary>
    Public Sub ResetToStand()
        HipPitchL = 0 : HipPitchR = 0
        HipRollL = 0 : HipRollR = 0
        KneeL = 0 : KneeR = 0
        AnkleL = 0 : AnkleR = 0
        ShoulderPitchL = 0 : ShoulderPitchR = 0
        ShoulderRollL = 0.06 : ShoulderRollR = -0.06
        ElbowL = 0.12 : ElbowR = 0.12
        SpinePitch = 0 : SpineRoll = 0 : SpineYaw = 0
        NeckPitch = 0 : NeckYaw = 0
        TargetSpeed = 0
        JumpSpeed = 0
        PelvisHeight = 0.98
    End Sub

    ''' <summary>
    ''' 把外部（神经网络）输出的连续动作向量写入本姿态。
    ''' 向量长度不足时缺失的分量保持原值，方便逐步扩展动作空间。
    ''' </summary>
    ''' <param name="action">按 <see cref="ActionDimension"/> 顺序排列的动作向量。</param>
    Public Sub FromActionVector(action As Single())
        If action Is Nothing OrElse action.Length = 0 Then
            Return
        End If

        Dim v As Double() = action.Select(Function(x) CDbl(x)).ToArray()

        If v.Length > 0 Then HipPitchL = v(0)
        If v.Length > 1 Then HipPitchR = v(1)
        If v.Length > 2 Then KneeL = v(2)
        If v.Length > 3 Then KneeR = v(3)
        If v.Length > 4 Then AnkleL = v(4)
        If v.Length > 5 Then AnkleR = v(5)
        If v.Length > 6 Then HipRollL = v(6)
        If v.Length > 7 Then HipRollR = v(7)
        If v.Length > 8 Then ShoulderPitchL = v(8)
        If v.Length > 9 Then ShoulderPitchR = v(9)
        If v.Length > 10 Then ElbowL = v(10)
        If v.Length > 11 Then ElbowR = v(11)
        If v.Length > 12 Then SpinePitch = v(12)
        If v.Length > 13 Then SpineRoll = v(13)
        If v.Length > 14 Then SpineYaw = v(14)
        If v.Length > 15 Then NeckYaw = v(15)
    End Sub

    ''' <summary><see cref="FromActionVector"/> 使用的动作向量维度。</summary>
    Public Shared ReadOnly Property ActionDimension As Integer
        Get
            Return 16
        End Get
    End Property

    Public Function Clone() As StickmanPose
        Return DirectCast(Me.MemberwiseClone(), StickmanPose)
    End Function
End Class
