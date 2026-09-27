Imports System.Globalization
Imports System.IO
Imports System.Math
Imports std = System.Math

''' <summary>
''' 一条训练样本：果蝇大脑的运动放电率特征 → 教师给出的 16 维关节偏置。
''' </summary>
Public Structure FlySample

    ''' <summary>运动神经元的滑动窗放电率（<see cref="FlyBrain.MotorFeatures"/>）。</summary>
    Public Features As Double()

    ''' <summary>教师给出的关节偏置（弧度，16 维）。</summary>
    Public Offsets As Double()

    ''' <summary>样本所属的 episode 序号（诊断用）。</summary>
    Public Episode As Integer
End Structure

''' <summary>
''' 线性回归读出层：把果蝇大脑运动神经元的放电率映射成火柴人的关节角度偏置。
''' </summary>
''' <remarks>
''' <para>
''' 对应贪吃蛇 demo 里的 <c>FlywireSnake.SnakeDecoder</c>——但那里是
''' <b>softmax 分类</b>（4 个离散方向），而火柴人需要的是<b>连续偏置</b>
''' （16 个关节角度，弧度），所以这里换成线性回归 + MSE 损失。
''' </para>
''' <para>
''' 模型：<c>y_o = b_o + Σ_i w_o·i · f_i</c>，
''' 其中 <c>f_i</c> 是第 i 个运动神经元的滑动窗放电率，<c>y_o</c> 是第 o 个关节的偏置。
''' 参数量只有 输出维 × 特征维（441 × 16 ≈ 7000），SGD 几轮就能收敛。
''' </para>
''' </remarks>
Public Class FlyRegressor

    Private ReadOnly m_weights As Double()()
    Private ReadOnly m_bias As Double()

    ''' <summary>输入维（运动神经元数）。</summary>
    Public ReadOnly Property Dimension As Integer

    ''' <summary>输出维（关节偏置个数 = 16）。</summary>
    Public ReadOnly Property OutputCount As Integer

    Public Sub New(dimension As Integer, outputCount As Integer)
        If dimension <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(dimension))
        If outputCount <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(outputCount))

        Dimension = dimension
        OutputCount = outputCount

        m_weights = New Double(outputCount - 1)() {}
        m_bias = New Double(outputCount - 1) {}

        ' 小的随机初始化：让初始输出接近 0（基准步态不被破坏）
        Dim rng As New Random(20260928)

        For o As Integer = 0 To outputCount - 1
            m_weights(o) = New Double(dimension - 1) {}

            For i As Integer = 0 To dimension - 1
                m_weights(o)(i) = (rng.NextDouble() - 0.5) * 0.02
            Next
        Next
    End Sub

    Private Sub New(dimension As Integer, outputCount As Integer,
                    weights As Double()(), bias As Double())
        Dimension = dimension
        OutputCount = outputCount
        m_weights = weights
        m_bias = bias
    End Sub

    ''' <summary>
    ''' 由放电率特征预测关节偏置（弧度）。
    ''' </summary>
    Public Function Predict(features As Double()) As Double()
        Dim result As Double() = New Double(OutputCount - 1) {}

        If features Is Nothing OrElse features.Length < Dimension Then
            ' 特征缺失（大脑还没推进过）时输出 0，即"完全跟随基准步态"
            Return result
        End If

        For o As Integer = 0 To OutputCount - 1
            Dim sum As Double = m_bias(o)
            Dim row As Double() = m_weights(o)

            For i As Integer = 0 To Dimension - 1
                sum += row(i) * features(i)
            Next

            result(o) = sum
        Next

        Return result
    End Function

    ''' <summary>
    ''' 梯度下降训练。
    ''' </summary>
    ''' <returns>训练后的平均绝对误差（弧度）。</returns>
    Public Function Train(samples As IList(Of FlySample),
                          Optional epochs As Integer = 8,
                          Optional rate As Double = 0.02,
                          Optional l2 As Double = 0.0001) As Double

        If samples Is Nothing OrElse samples.Count = 0 Then
            Return 0.0
        End If

        ' 只使用特征维数正确的样本（大脑装配参数变了之后旧样本要丢弃）
        Dim usable As New List(Of FlySample)()

        For Each s As FlySample In samples
            If s.Features IsNot Nothing AndAlso s.Features.Length = Dimension AndAlso
               s.Offsets IsNot Nothing AndAlso s.Offsets.Length = OutputCount Then
                Call usable.Add(s)
            End If
        Next

        If usable.Count = 0 Then
            Return 0.0
        End If

        Dim [error] As Double = 0.0

        For epoch As Integer = 1 To Math.Max(1, epochs)
            Dim errorSum As Double = 0.0

            For Each s As FlySample In usable
                Dim y As Double() = Predict(s.Features)
                Dim scale As Double = rate / usable.Count

                For o As Integer = 0 To OutputCount - 1
                    Dim residual As Double = y(o) - s.Offsets(o)

                    errorSum += std.Abs(residual)

                    ' 权重更新（MSE 的梯度 + L2 正则）
                    Dim row As Double() = m_weights(o)
                    Dim g As Double = 2.0 * residual * scale

                    For i As Integer = 0 To Dimension - 1
                        row(i) -= g * s.Features(i) + l2 * row(i) * scale
                    Next

                    m_bias(o) -= 2.0 * residual * scale
                Next
            Next

            [error] = errorSum / (usable.Count * OutputCount)
        Next

        Return [error]
    End Function

    ''' <summary>在样本集上的平均绝对误差（弧度）。</summary>
    Public Function Evaluate(samples As IList(Of FlySample)) As Double
        If samples Is Nothing OrElse samples.Count = 0 Then
            Return 0.0
        End If

        Dim sum As Double = 0.0
        Dim count As Integer = 0

        For Each s As FlySample In samples
            If s.Features Is Nothing OrElse s.Features.Length <> Dimension OrElse
               s.Offsets Is Nothing OrElse s.Offsets.Length <> OutputCount Then
                Continue For
            End If

            Dim y As Double() = Predict(s.Features)

            For o As Integer = 0 To OutputCount - 1
                sum += std.Abs(y(o) - s.Offsets(o))
                count += 1
            Next
        Next

        If count = 0 Then
            Return 0.0
        End If

        Return sum / count
    End Function

    ''' <summary>诊断摘要（训练日志用）。</summary>
    Public Function Describe() As String
        Dim total As Double = 0.0
        Dim nonzero As Integer = 0

        For o As Integer = 0 To OutputCount - 1
            For i As Integer = 0 To Dimension - 1
                Dim w As Double = m_weights(o)(i)

                total += w * w

                If std.Abs(w) > 0.0005 Then
                    nonzero += 1
                End If
            Next
        Next

        Return $"in={Dimension}, out={OutputCount}, |w|={Sqrt(total):F4}, nonzero={nonzero}"
    End Function

#Region "存读"

    ''' <summary>把权重存成 csv（文本格式，方便人工检查）。</summary>
    Public Sub Save(path As String)
        Dim sb As New System.Text.StringBuilder()

        Call sb.AppendLine("# flywire-stickman brain readout v1")
        Call sb.AppendLine($"# saved={DateTime.Now:yyyy-MM-dd HH:mm:ss}")
        Call sb.AppendLine($"dimension,{Dimension}")
        Call sb.AppendLine($"outputs,{OutputCount}")

        For o As Integer = 0 To OutputCount - 1
            Call sb.AppendLine($"b,{o},{m_bias(o).ToString("R", CultureInfo.InvariantCulture)}")

            For i As Integer = 0 To Dimension - 1
                Call sb.AppendLine($"w,{o},{i},{m_weights(o)(i).ToString("R", CultureInfo.InvariantCulture)}")
            Next
        Next

        Call File.WriteAllText(path, sb.ToString())
    End Sub

    ''' <summary>从 csv 读回权重。</summary>
    Public Shared Function Load(path As String) As FlyRegressor
        If Not File.Exists(path) Then
            Throw New FileNotFoundException("读出层权重文件不存在", path)
        End If

        Dim dimension As Integer = -1
        Dim outputs As Integer = -1
        Dim weights As Double()() = Nothing
        Dim bias As Double() = Nothing

        For Each line As String In File.ReadAllLines(path)
            If line Is Nothing OrElse line.Length = 0 OrElse line.StartsWith("#") Then
                Continue For
            End If

            Dim parts As String() = line.Split(","c)

            If parts.Length < 2 Then
                Continue For
            End If

            Select Case parts(0).Trim().ToLowerInvariant()
                Case "dimension"
                    dimension = Integer.Parse(parts(1).Trim(), CultureInfo.InvariantCulture)
                Case "outputs"
                    outputs = Integer.Parse(parts(1).Trim(), CultureInfo.InvariantCulture)
                Case "b"
                    If bias Is Nothing AndAlso dimension > 0 AndAlso outputs > 0 Then
                        bias = New Double(outputs - 1) {}
                    End If

                    bias(Integer.Parse(parts(1).Trim(), CultureInfo.InvariantCulture)) =
                        Double.Parse(parts(2).Trim(), CultureInfo.InvariantCulture)
                Case "w"
                    If weights Is Nothing AndAlso dimension > 0 AndAlso outputs > 0 Then
                        weights = New Double(outputs - 1)() {}

                        For o As Integer = 0 To outputs - 1
                            weights(o) = New Double(dimension - 1) {}
                        Next
                    End If

                    weights(Integer.Parse(parts(1).Trim(), CultureInfo.InvariantCulture))(
                        Integer.Parse(parts(2).Trim(), CultureInfo.InvariantCulture)) =
                        Double.Parse(parts(3).Trim(), CultureInfo.InvariantCulture)
            End Select
        Next

        If dimension <= 0 OrElse outputs <= 0 OrElse weights Is Nothing OrElse bias Is Nothing Then
            Throw New InvalidDataException($"读出层权重文件不完整: {path}")
        End If

        Return New FlyRegressor(dimension, outputs, weights, bias)
    End Function

#End Region

End Class

''' <summary>
''' 连续输出的平滑 / 限幅滤波器。
''' </summary>
''' <remarks>
''' 贪吃蛇 demo 的 <c>FlywireSnake.SnakeDecisionFilter</c> 是给离散动作用的
''' （指数平滑 + 转向迟滞 + 冷却）；连续关节偏置需要的是另一套：
''' 一阶低通（抑制放电率的逐 tick 抖动）+ 变化率限幅（关节不能瞬移）+ 死区（读出噪声归零）。
''' </remarks>
Public Class FlyDecisionFilter

    ''' <summary>一阶低通系数（0 = 不平滑，越大越平滑）。默认 0.35。</summary>
    Public Property Smoothing As Double = 0.35

    ''' <summary>每次大脑 tick 的最大偏置变化量（弧度）。默认 0.35。</summary>
    Public Property MaxDelta As Double = 0.35

    ''' <summary>死区阈值：绝对值小于它的输出分量直接归零。</summary>
    Public Property DeadZone As Double = 0.015

    ''' <summary>输出限幅（弧度），与 <see cref="FigureEnvironment.OffsetLimit"/> 对齐。</summary>
    Public Property OutputLimit As Double = 1.2

    Private m_state As Double() = New Double() {}

    ''' <summary>复位内部状态（角色复位时调用）。</summary>
    Public Sub Reset()
        m_state = New Double() {}
    End Sub

    ''' <summary>
    ''' 滤波一帧原始偏置，返回实际下发给火柴人的偏置。
    ''' </summary>
    ''' <param name="raw">回归器输出的原始偏置（弧度）。</param>
    ''' <param name="dt">两次大脑 tick 之间的间隔（秒）。</param>
    Public Function Filter(raw As Double(), dt As Double) As Double()
        If raw Is Nothing OrElse raw.Length = 0 Then
            Return New Double() {}
        End If

        If m_state.Length <> raw.Length Then
            m_state = New Double(raw.Length - 1) {}
        End If

        Dim step_ As Double = If(dt > 0, dt, 0.016)
        Dim rate As Double = Clamp01(Smoothing)
        Dim out As Double() = New Double(raw.Length - 1) {}

        For i As Integer = 0 To raw.Length - 1
            Dim target As Double = Sanitize(raw(i))

            ' 死区
            If std.Abs(target) < DeadZone Then
                target = 0.0
            End If

            ' 一阶低通
            Dim smoothed As Double = m_state(i) + (target - m_state(i)) * rate

            ' 变化率限幅（关节不能瞬移）
            Dim maxStep As Double = MaxDelta * std.Max(0.05, step_ / 0.066)
            Dim delta As Double = smoothed - m_state(i)

            If std.Abs(delta) > maxStep Then
                delta = std.Sign(delta) * maxStep
            End If

            Dim value As Double = Clamp(m_state(i) + delta, OutputLimit)

            m_state(i) = value
            out(i) = value
        Next

        Return out
    End Function

    Private Shared Function Sanitize(v As Double) As Double
        If Double.IsNaN(v) OrElse Double.IsInfinity(v) Then
            Return 0.0
        End If

        Return v
    End Function

    Private Shared Function Clamp(v As Double, limit As Double) As Double
        Return std.Min(limit, std.Max(-limit, v))
    End Function

    Private Shared Function Clamp01(v As Double) As Double
        Return std.Min(1.0, std.Max(0.0, v))
    End Function

End Class
