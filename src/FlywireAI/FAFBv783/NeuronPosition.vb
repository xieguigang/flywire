Imports System.Globalization

Namespace FAFBv783

    ''' <summary>
    ''' 一个神经元在 FAFB 坐标系之中的位置 (单位：纳米)。
    ''' </summary>
    ''' <remarks>
    ''' 与 ``Point3D`` 分开定义是为了让数据层不依赖绘图层：三维可视化、统计分析、
    ''' 以及将来的空间聚类都只需要这三个 ``Double``。
    ''' FAFB v783 的坐标范围约为 ``[0, 800000]`` 纳米 (即 0.8 毫米)，因此单精度浮点
    ''' 仍然可以保留 0.03 纳米级别的精度，但这里保存为 ``Double`` 以免在数据层丢精度。
    ''' </remarks>
    Public Structure NeuronPosition

        Public Sub New(x As Double, y As Double, z As Double)
            Me.X = x
            Me.Y = y
            Me.Z = z
        End Sub

        ''' <summary>X 坐标 (纳米，左右方向)。</summary>
        Public Property X As Double

        ''' <summary>Y 坐标 (纳米，前后方向)。</summary>
        Public Property Y As Double

        ''' <summary>Z 坐标 (纳米，背腹方向)。</summary>
        Public Property Z As Double

        ''' <summary>该位置是否为有效值 (``coordinates.csv`` 只覆盖了一部分标记位置)。</summary>
        Public ReadOnly Property IsValid As Boolean
            Get
                Return Not (Double.IsNaN(X) OrElse Double.IsNaN(Y) OrElse Double.IsNaN(Z))
            End Get
        End Property

        ''' <summary>坐标是否为全零 (例如缺少坐标时的占位值)。</summary>
        Public ReadOnly Property IsOrigin As Boolean
            Get
                Return X = 0 AndAlso Y = 0 AndAlso Z = 0
            End Get
        End Property

        Public Overrides Function ToString() As String
            If Not IsValid Then
                Return "(no position)"
            End If

            Return $"({X:N0}, {Y:N0}, {Z:N0})"
        End Function

    End Structure

    ''' <summary>
    ''' ``coordinates.csv`` 的 ``position`` 列 (形如 ``[352484 175164 229040]``) 的解析器。
    ''' </summary>
    ''' <remarks>
    ''' 该列是文本形式的三个整数，用空格分隔并且用方括号包起来，且各列的数字宽度
    ''' 是对齐的 (因此存在<b>连续空格</b>与前导空格)。这里按"去掉方括号 → 按空白切分"
    ''' 的方式解析，并且额外容忍逗号分隔的写法，以便同一段代码也能读取手工导出的
    ''' 坐标表格。
    ''' </remarks>
    Public Module PositionParser

        Private ReadOnly Separators As Char() = {" "c, ChrW(9), ","c, ";"c}

        ''' <summary>
        ''' 解析一个 ``[x y z]`` 形式的坐标文本。
        ''' </summary>
        ''' <param name="text">坐标文本</param>
        ''' <param name="position">解析结果</param>
        ''' <returns>文本为空、格式不符或者数值无法解析时返回 ``False``</returns>
        Public Function TryParse(text As String, ByRef position As NeuronPosition) As Boolean
            position = Nothing

            If String.IsNullOrWhiteSpace(text) Then
                Return False
            End If

            Dim content As String = text.Trim()

            If content.StartsWith("["c) Then
                content = content.Substring(1)
            End If
            If content.EndsWith("]"c) Then
                content = content.Substring(0, content.Length - 1)
            End If

            Dim parts As String() = content.Split(Separators, StringSplitOptions.RemoveEmptyEntries)

            If parts.Length < 3 Then
                Return False
            End If

            Dim x As Double
            Dim y As Double
            Dim z As Double

            If Not Double.TryParse(parts(0), NumberStyles.Float, CultureInfo.InvariantCulture, x) Then
                Return False
            End If
            If Not Double.TryParse(parts(1), NumberStyles.Float, CultureInfo.InvariantCulture, y) Then
                Return False
            End If
            If Not Double.TryParse(parts(2), NumberStyles.Float, CultureInfo.InvariantCulture, z) Then
                Return False
            End If

            position = New NeuronPosition(x, y, z)

            Return True
        End Function

        ''' <summary>
        ''' 解析一个 ``[x y z]`` 形式的坐标文本，格式不符时抛出异常。
        ''' </summary>
        ''' <exception cref="FormatException">文本不是一个有效的三维坐标</exception>
        Public Function Parse(text As String) As NeuronPosition
            Dim position As NeuronPosition

            If Not TryParse(text, position) Then
                Throw New FormatException($"无效的坐标文本: '{text}'")
            End If

            Return position
        End Function

    End Module

End Namespace
