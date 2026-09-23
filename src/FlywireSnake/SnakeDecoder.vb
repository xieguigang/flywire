Imports System.Globalization
Imports System.IO
Imports System.Text


    ''' <summary>一条训练样本：运动神经元特征 + 示范动作。</summary>
    Public Structure SnakeSample
        ''' <summary>特征向量（运动神经元的滑动窗放电率，末尾通常是偏置项）。</summary>
        Public Features As Double()

        ''' <summary>示范动作编号（0=上 1=下 2=左 3=右）。</summary>
        Public Action As Integer

        ''' <summary>当前回合编号（用于把样本按回合切分做验证）。</summary>
        Public Episode As Integer
    End Structure

    ''' <summary>
    ''' 运动解码器：把果蝇大脑运动神经元的放电率线性映射到 4 个动作。
    ''' </summary>
    ''' <remarks>
    ''' <b>为什么需要一个"解码器"</b>：这份连接组没有标注过任何"往左走"的运动语义，
    ''' 运动神经元的活动是一团高维信号。解码器在这里扮演的是<b>脑机接口</b>的角色 ——
    ''' 用一个线性读出把大脑状态翻译成设备指令（这正是神经假体里的标准做法）。
    ''' 
    ''' 训练方式是<b>模仿学习</b>：先用一个贪心追食物的教师策略玩若干回合，
    ''' 记录每个 tick 的（运动神经元活动 → 教师动作），再用 softmax 交叉熵训练权重。
    ''' 训练完成后由大脑的实时活动决定蛇的走向，教师不再参与。
    ''' </remarks>
    Public Class SnakeDecoder

        Private ReadOnly m_weights As Double()()      ' [action][feature]
        Private m_rng As Random

        ''' <summary>特征维度。</summary>
        Public ReadOnly Property FeatureCount As Integer

        ''' <summary>动作数。</summary>
        Public ReadOnly Property ActionCount As Integer

        ''' <summary>训练样本数（诊断）。</summary>
        Public Property TrainedSamples As Integer

        ''' <summary>训练集上的准确率（诊断）。</summary>
        Public Property TrainAccuracy As Double

        ''' <remarks>
        ''' 形参名避开 <c>featureCount</c>：VB 不区分大小写，与 <see cref="FeatureCount"/> 属性
        ''' 同名会让"赋值"变成自赋值，特征维度永远是 0。
        ''' </remarks>
        Public Sub New(dimension As Integer, Optional seed As Integer = 20180923)
            If dimension <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(dimension))

            FeatureCount = dimension
            ActionCount = SnakeSensorEncoder.ActionCount
            m_rng = New Random(seed)
            m_weights = New Double(ActionCount - 1)() {}

            ' 小随机初值：全零会让所有动作得分相同、梯度完全对称，学不动
            For a As Integer = 0 To ActionCount - 1
                Dim row As Double() = New Double(dimension - 1) {}

                For i As Integer = 0 To dimension - 1
                    row(i) = (m_rng.NextDouble() - 0.5) * 0.01
                Next

                m_weights(a) = row
            Next
        End Sub

        ''' <summary>取权重（诊断 / 保存）。</summary>
        Public Function Weights() As Double()()
            Return m_weights
        End Function

        ''' <summary>各动作的打分。</summary>
        ''' <param name="features">特征向量（长度必须等于 <see cref="FeatureCount"/>）</param>
        ''' <param name="forbidden">禁止的动作（蛇不能反向），设为 -∞</param>
        ''' <remarks>
        ''' 局部变量不能叫 <c>scores</c>：与函数同名（VB 不区分大小写）。
        ''' </remarks>
        Public Function Scores(features As Double(), Optional forbidden As Integer = -1) As Double()
            Dim result As Double() = New Double(ActionCount - 1) {}

            For a As Integer = 0 To ActionCount - 1
                If a = forbidden Then
                    result(a) = Double.NegativeInfinity
                    Continue For
                End If

                Dim row As Double() = m_weights(a)
                Dim sum As Double = 0
                Dim count As Integer = Math.Min(row.Length, If(features Is Nothing, 0, features.Length))

                For i As Integer = 0 To count - 1
                    sum += row(i) * features(i)
                Next

                result(a) = sum
            Next

            Return result
        End Function

        ''' <summary>按打分挑一个动作。</summary>
        Public Function Decide(features As Double(), Optional forbidden As Integer = -1) As Integer
            Dim result As Double() = Scores(features, forbidden)
            Dim best As Integer = -1
            Dim bestScore As Double = Double.NegativeInfinity

            For a As Integer = 0 To result.Length - 1
                If result(a) > bestScore Then
                    bestScore = result(a)
                    best = a
                End If
            Next

            If best < 0 Then
                ' 全部被禁（理论上只有"4 个方向都被禁"才会出现）：保持原方向
                Return If(forbidden = 0, 1, 0)
            End If

            Return best
        End Function

        ''' <summary>
        ''' 用样本训练（softmax 交叉熵 + 小步长 SGD）。
        ''' </summary>
        ''' <param name="samples">训练样本</param>
        ''' <param name="epochs">迭代轮数</param>
        ''' <param name="rate">学习率</param>
        ''' <param name="l2">L2 正则（防止权重发散）</param>
        ''' <returns>训练集准确率</returns>
        Public Function Train(samples As IList(Of SnakeSample),
                              Optional epochs As Integer = 12,
                              Optional rate As Double = 0.35,
                              Optional l2 As Double = 1.0E-4) As Double

            If samples Is Nothing OrElse samples.Count = 0 Then Return 0

            Dim order As Integer() = Enumerable.Range(0, samples.Count).ToArray()
            Dim gradient As Double()() = New Double(ActionCount - 1)() {}
            Dim logits As Double() = New Double(ActionCount - 1) {}
            Dim probability As Double() = New Double(ActionCount - 1) {}

            For a As Integer = 0 To ActionCount - 1
                gradient(a) = New Double(FeatureCount - 1) {}
            Next

            For epoch As Integer = 1 To Math.Max(1, epochs)
                shuffle(order)

                For Each index As Integer In order
                    Dim sample As SnakeSample = samples(index)
                    Dim features As Double() = sample.Features

                    If features Is Nothing OrElse features.Length <> FeatureCount Then Continue For

                    ' ---- 前向：logits → softmax ----
                    Dim max As Double = Double.NegativeInfinity

                    For a As Integer = 0 To ActionCount - 1
                        Dim sum As Double = 0
                        Dim row As Double() = m_weights(a)

                        For i As Integer = 0 To FeatureCount - 1
                            sum += row(i) * features(i)
                        Next

                        logits(a) = sum
                        If sum > max Then max = sum
                    Next

                    Dim total As Double = 0

                    For a As Integer = 0 To ActionCount - 1
                        probability(a) = Math.Exp(logits(a) - max)
                        total += probability(a)
                    Next

                    For a As Integer = 0 To ActionCount - 1
                        probability(a) /= Math.Max(total, 1.0E-12)
                    Next

                    ' ---- 反向：交叉熵对权重的梯度 = (p - onehot) * feature ----
                    For a As Integer = 0 To ActionCount - 1
                        Dim delta As Double = probability(a) - If(a = sample.Action, 1.0, 0.0)
                        Dim row As Double() = m_weights(a)
                        Dim g As Double() = gradient(a)

                        For i As Integer = 0 To FeatureCount - 1
                            g(i) = delta * features(i) + l2 * row(i)
                        Next
                    Next

                    ' ---- 更新 ----
                    For a As Integer = 0 To ActionCount - 1
                        Dim row As Double() = m_weights(a)
                        Dim g As Double() = gradient(a)

                        For i As Integer = 0 To FeatureCount - 1
                            row(i) -= rate * g(i)
                        Next
                    Next
                Next
            Next

            TrainedSamples = samples.Count
            TrainAccuracy = Accuracy(samples)

            Return TrainAccuracy
        End Function

        ''' <summary>样本上的准确率（与教师动作的一致率）。</summary>
        Public Function Accuracy(samples As IList(Of SnakeSample)) As Double
            If samples Is Nothing OrElse samples.Count = 0 Then Return 0

            Dim hit As Integer = 0

            For Each sample As SnakeSample In samples
                If sample.Features Is Nothing OrElse sample.Features.Length <> FeatureCount Then Continue For

                If Decide(sample.Features) = sample.Action Then hit += 1
            Next

            Return hit / samples.Count
        End Function

        Private Sub shuffle(order As Integer())
            For i As Integer = order.Length - 1 To 1 Step -1
                Dim j As Integer = m_rng.Next(i + 1)
                Dim swap As Integer = order(i)

                order(i) = order(j)
                order(j) = swap
            Next
        End Sub

#Region "持久化"

        ''' <summary>把权重写成 csv（首行为 ""action,feature_count""，其后每行一个动作）。</summary>
        Public Sub Save(file As String)
            Dim sb As New StringBuilder()

            Call sb.AppendLine("action,feature_count,bias_index")
            Call sb.AppendLine($"#,{FeatureCount},{FeatureCount - 1}")

            For a As Integer = 0 To ActionCount - 1
                Dim cells As New List(Of String) From {a.ToString}

                For Each w As Double In m_weights(a)
                    Call cells.Add(w.ToString("G9", CultureInfo.InvariantCulture))
                Next

                Call sb.AppendLine(String.Join(",", cells))
            Next

            Call IO.File.WriteAllText(file, sb.ToString, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
        End Sub

        ''' <summary>从 csv 读回权重（特征维度必须一致）。</summary>
        Public Shared Function Load(file As String, featureCount As Integer) As SnakeDecoder
            Dim decoder As New SnakeDecoder(featureCount)
            Dim rows As New List(Of Double())()

            For Each line As String In IO.File.ReadLines(file)
                Dim cells As String() = line.Split(","c)

                If cells.Length < 2 Then Continue For

                Dim action As Integer

                If Not Integer.TryParse(cells(0), action) Then Continue For
                If cells.Length - 1 <> featureCount Then Continue For

                Dim row As Double() = New Double(featureCount - 1) {}

                For i As Integer = 1 To cells.Length - 1
                    Double.TryParse(cells(i), NumberStyles.Float, CultureInfo.InvariantCulture, row(i - 1))
                Next

                Call rows.Add(row)
            Next

            If rows.Count <> decoder.ActionCount Then
                Throw New InvalidDataException($"解码器权重文件里的动作数（{rows.Count}）与预期（{decoder.ActionCount}）不一致")
            End If

            For a As Integer = 0 To decoder.ActionCount - 1
                Array.Copy(rows(a), decoder.m_weights(a), featureCount)
            Next

            Return decoder
        End Function

#End Region

    End Class

