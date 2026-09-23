Imports System.Drawing

Namespace FlywireSnake

    ''' <summary>
    ''' 感觉通道：把贪吃蛇的游戏画面折算成一组"外界刺激强度"。
    ''' </summary>
    ''' <remarks>
    ''' <b>为什么是这 16 个通道</b>：一条只知道往前走的蛇，决策只需要两类信息 ——
    ''' "食物在哪个方位"（要追）与"往哪走会死"（要躲）。因此：
    ''' <list type="bullet">
    '''   <item>0..7：<b>食物方位</b>，以蛇的<b>当前朝向</b>为参考系的 8 个扇区
    '''         （前进方向是扇区 0，左右各 3 个，正后方 7）。用相对朝向而不是绝对方向，
    '''         是因为蛇不能反向移动，"前方 / 左侧 / 右侧"才是可学习的刺激量；</item>
    '''   <item>8..11：<b>碰撞危险</b>，上 / 下 / 左 / 右四个绝对方向
    '''         （墙体、障碍物、自己与 AI 蛇的身体）；</item>
    '''   <item>12..15：<b>活动食物方位</b>，同样以朝向为参考系的 4 个粗扇区。</item>
    ''' </list>
    ''' 
    ''' 强度约定：食物通道取"最近的那份食物"的距离衰减 <c>1 - d / R</c>（R = 可视半径），
    ''' 危险通道取 0 / 1。这些强度最终会乘上注入电流标定值变成感觉神经元的电流。
    ''' </remarks>
    Public NotInheritable Class SnakeSensors

        ''' <summary>食物方位扇区数。</summary>
        Public Const FoodSectors As Integer = 8

        ''' <summary>碰撞危险通道数（上 / 下 / 左 / 右）。</summary>
        Public Const DangerChannels As Integer = 4

        ''' <summary>活动食物方位扇区数。</summary>
        Public Const MovingFoodSectors As Integer = 4

        ''' <summary>感觉通道总数。</summary>
        Public Const ChannelCount As Integer = FoodSectors + DangerChannels + MovingFoodSectors

        ''' <summary>食物感知半径（格）。超出这个距离的食物不再产生刺激。</summary>
        Public Const SenseRadius As Double = 45.0

        ''' <summary>四个绝对方向（上 / 下 / 左 / 右），与危险通道 8..11 一一对应。</summary>
        Public Shared ReadOnly Directions As Point() = {
            New Point(0, -1), New Point(0, 1), New Point(-1, 0), New Point(1, 0)
        }

        ''' <summary>某个通道的名字（用于可视化与报告）。</summary>
        Public Shared Function ChannelName(channel As Integer) As String
            If channel < FoodSectors Then
                Return $"食物方位 {channel}"
            ElseIf channel < FoodSectors + DangerChannels Then
                Dim part As String() = {"上", "下", "左", "右"}
                Return $"危险 {part(channel - FoodSectors)}"
            Else
                Dim part As String() = {"前方", "左方", "右方", "后方"}
                Return $"活动食物 {part(channel - FoodSectors - DangerChannels)}"
            End If
        End Function

        ''' <summary>据蛇的朝向把相对方位（前方 / 左 / 右 / 后）折算成绝对偏移。</summary>
        ''' <remarks>
        ''' 蛇的朝向只可能是四个轴向之一，因此"左转 / 右转"用简单的轴向旋转即可：
        ''' 屏幕坐标 Y 轴向下，左转 = 把 (x, y) 变成 (y, -x)。
        ''' </remarks>
        Public Shared Function Relative(heading As Point, offset As Point) As Point
            Dim forward As Point = If(heading = Point.Empty, New Point(1, 0), heading)

            If offset.X <> 0 Then
                ' 沿当前朝向的前 / 后
                Return New Point(forward.X * offset.X, forward.Y * offset.X)
            End If

            ' 左 / 右：把朝向旋转 ±90°
            Dim lateral As New Point(forward.Y, -forward.X)   ' 右

            If offset.Y > 0 Then
                lateral = New Point(-lateral.X, -lateral.Y)   ' 左
            End If

            Return lateral
        End Function

    End Class

    ''' <summary>
    ''' 一次感觉采样的结果：通道强度 + 用于训练解码器的"示范动作"。
    ''' </summary>
    Public Structure SnakeSensorFrame
        ''' <summary>16 个通道的强度（[0,1]）。</summary>
        Public Values As Double()

        ''' <summary>被蛇身体 / 障碍物 / 墙占据的格子数（诊断用）。</summary>
        Public BlockedCells As Integer

        ''' <summary>距离最近食物的曼哈顿距离（没有食物时为 -1）。</summary>
        Public FoodDistance As Integer
    End Structure

End Namespace
