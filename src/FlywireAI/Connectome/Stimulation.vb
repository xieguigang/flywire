Imports System.Linq
Imports std = System.Math
Imports Microsoft.VisualBasic.MachineLearning.TensorFlow

Namespace Connectome

    ''' <summary>
    ''' 外部驱动方案：把 ``inputSize`` 个输入特征映射到目标神经元 (``inputMap``，供
    ''' <c>SpikingNetwork.AddSparseLayer</c> 使用)，并给出每个特征的输入强度 (取值 [0,1])。
    ''' </summary>
    ''' <remarks>
    ''' 两种模式：
    ''' 
    ''' * <see cref="StimulationMode.RandomNeurons"/>：按固定种子随机挑选一批神经元；
    ''' * <see cref="StimulationMode.CellType"/>：按照 ``group`` / ``class`` / ``primary_type``
    '''   注释筛选神经元 (未指定条件的时候自动挑选数量最多的 group)。
    ''' 
    ''' 由于 ``inputMap`` 把少量输入特征直接注入到目标神经元，输入张量的宽度就是刺激神经元数
    ''' (默认 5,000)，而不是全脑的 139,255。
    ''' </remarks>
    Public Class Stimulation

        ''' <summary>驱动模式。</summary>
        Public ReadOnly Property Mode As StimulationMode

        ''' <summary>输入特征 -> 神经元索引 的注入映射。</summary>
        Public ReadOnly Property InputMap As Integer()

        ''' <summary>每个输入特征的强度 (长度与 <see cref="InputMap"/> 一致，取值 [0,1])。</summary>
        Public ReadOnly Property Values As Double()

        ''' <summary>被刺激神经元所对应的 root_id。</summary>
        Public ReadOnly Property RootIds As Long()

        ''' <summary>刺激方案的描述文本 (用于控制台报告与结果文件名)。</summary>
        Public ReadOnly Property Label As String

        ''' <summary>输入特征数量 (即 ``inputSize``)。</summary>
        Public ReadOnly Property Count As Integer
            Get
                Return InputMap.Length
            End Get
        End Property

        Private Sub New(mode As StimulationMode,
                        inputMap As Integer(),
                        values As Double(),
                        rootIds As Long(),
                        label As String)

            Me.Mode = mode
            Me.InputMap = inputMap
            Me.Values = values
            Me.RootIds = rootIds
            Me.Label = label
        End Sub

        ''' <summary>
        ''' 创建形状为 ``[1, Count]`` 的输入张量。
        ''' </summary>
        Public Function CreateInputTensor() As Tensor
            Return New Tensor(Values, 1, Count)
        End Function

        ''' <summary>
        ''' 按照配置创建驱动方案。
        ''' </summary>
        Public Shared Function Create(config As SnnConfig, index As ConnectomeIndex) As Stimulation
            If config Is Nothing Then
                Throw New ArgumentNullException(NameOf(config))
            End If
            If index Is Nothing Then
                Throw New ArgumentNullException(NameOf(index))
            End If

            Select Case config.Mode
                Case StimulationMode.CellType
                    Return ByCellType(config, index)
                Case Else
                    Return RandomNeurons(config, index)
            End Select
        End Function

        ''' <summary>
        ''' 只刺激一个指定的神经元，注入强度由调用方给定。
        ''' </summary>
        ''' <param name="index">神经元索引</param>
        ''' <param name="neuron">被刺激的神经元索引</param>
        ''' <param name="strength">注入电流强度 (与膜电位阈值同一量纲)</param>
        ''' <param name="label">方案描述 (留空时自动生成)</param>
        ''' <remarks>
        ''' 用于交互式电刺激：鼠标点中哪个神经元，就给哪个神经元注入电流，
        ''' 强度由"按住左键的时长"映射而来。
        ''' 
        ''' 注入路径与批量刺激完全一致（<c>scatter</c> 把第 i 个特征的强度加到
        ''' <c>InputMap(i)</c> 指定的神经元上），因此这里的 ``inputSize`` 就是 1 ——
        ''' 网络只为一个神经元建一个输入特征，装配代价可以忽略。
        ''' 
        ''' 强度允许大于 1：注入的是<b>电流</b>而不是发放率，LIF 的阈值由
        ''' <c>SnnConfig.Threshold</c> 给出（默认 1.0）。注入超过阈值的电流会让该神经元
        ''' 立即发放；在恒流编码下每步都注入，于是它就按 β 的泄漏节奏持续发放 ——
        ''' 这正是"电极持续放电"的语义。
        ''' </remarks>
        Public Shared Function CreateSingle(index As ConnectomeIndex,
                                            neuron As Integer,
                                            strength As Double,
                                            Optional label As String = Nothing) As Stimulation

            If index Is Nothing Then
                Throw New ArgumentNullException(NameOf(index))
            End If
            If neuron < 0 OrElse neuron >= index.Size Then
                Throw New ArgumentOutOfRangeException(NameOf(neuron), neuron, $"神经元索引应落在 [0, {index.Size})")
            End If
            If strength <= 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(strength), strength, "刺激强度必须为正数")
            End If

            Dim rootId As Long = index.GetRootId(neuron)

            If String.IsNullOrWhiteSpace(label) Then
                label = $"single neuron #{neuron} (root_id {rootId}) @ {strength:F2}"
            End If

            Return New Stimulation(
                StimulationMode.RandomNeurons,
                New Integer() {neuron},
                New Double() {strength},
                New Long() {rootId},
                label)
        End Function

        ''' <summary>
        ''' 对<b>一组</b>指定神经元注入同一强度的电流（"电极附近被募集的一群神经元"）。
        ''' </summary>
        ''' <param name="index">神经元索引</param>
        ''' <param name="neurons">被募集的神经元索引</param>
        ''' <param name="strength">注入电流强度（与膜电位阈值同量纲）</param>
        ''' <param name="label">方案描述</param>
        ''' <remarks>
        ''' <b>为什么电刺激必须是一群而不是一个</b>
        ''' 
        ''' 本数据集的权重经过结构归一化：每个神经元的总输入幅度被缩放到约一个阈值，
        ''' 因此<b>单个</b>突触前神经元的一次发放对突触后的贡献只有 1/扇入 量级。
        ''' 实测（全脑 139,255 神经元 / 373 万突触）单个神经元即使持续注入 16 倍阈值电流，
        ''' 也只激活它自己（active=1，无任何传播）—— 这是网络动力学的正确结果，不是缺陷。
        ''' 
        ''' 真实电极也是如此：电流从电极尖端扩散，同时兴奋附近的一群神经元
        ''' （微刺激实验里"刺激强度"指的就是被募集的组织范围）。
        ''' 因此这里的刺激方案 = <b>点击位置附近的一群神经元 + 每点一个注入电流</b>。
        ''' </remarks>
        Public Shared Function CreatePatch(index As ConnectomeIndex,
                                           neurons As Integer(),
                                           strength As Double,
                                           Optional label As String = Nothing) As Stimulation

            If index Is Nothing Then
                Throw New ArgumentNullException(NameOf(index))
            End If
            If neurons Is Nothing OrElse neurons.Length = 0 Then
                Throw New ArgumentException("被募集的神经元不能为空", NameOf(neurons))
            End If
            If strength <= 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(strength), strength, "刺激强度必须为正数")
            End If

            Dim values As Double() = New Double(neurons.Length - 1) {}
            Dim rootIds As Long() = New Long(neurons.Length - 1) {}

            For i As Integer = 0 To neurons.Length - 1
                If neurons(i) < 0 OrElse neurons(i) >= index.Size Then
                    Throw New ArgumentOutOfRangeException(NameOf(neurons), neurons(i), $"神经元索引应落在 [0, {index.Size})")
                End If

                values(i) = strength
                rootIds(i) = index.GetRootId(neurons(i))
            Next

            If String.IsNullOrWhiteSpace(label) Then
                label = $"{neurons.Length} recruited neurons @ {strength:F2}"
            End If

            Return New Stimulation(StimulationMode.RandomNeurons, neurons, values, rootIds, label)
        End Function

        ''' <summary>
        ''' 随机挑选一批神经元进行刺激。
        ''' </summary>
        Public Shared Function RandomNeurons(config As SnnConfig, index As ConnectomeIndex) As Stimulation
            Dim neurons As Integer = index.Size
            Dim count As Integer = std.Min(config.StimulationNeurons, neurons)
            Dim rng As New Random(config.Seed)
            Dim picked As New HashSet(Of Integer)()
            Dim selected As New List(Of Integer)(count)

            While selected.Count < count
                Dim i As Integer = rng.Next(0, neurons)

                If picked.Add(i) Then
                    Call selected.Add(i)
                End If
            End While

            Call selected.Sort()

            Return build(config, index, StimulationMode.RandomNeurons, selected.ToArray,
                         $"random neurons (seed={config.Seed})")
        End Function

        ''' <summary>
        ''' 按照 ``group`` / ``class`` / ``primary_type`` 筛选神经元进行刺激。
        ''' </summary>
        Public Shared Function ByCellType(config As SnnConfig, index As ConnectomeIndex) As Stimulation
            Dim targetGroup As String = If(config.TargetGroup, "").Trim
            Dim targetClass As String = If(config.TargetClass, "").Trim
            Dim targetType As String = If(config.TargetPrimaryType, "").Trim

            If targetGroup.Length = 0 AndAlso targetClass.Length = 0 AndAlso targetType.Length = 0 Then
                ' 没有指定筛选条件的时候，自动挑选数量最多的 group
                targetGroup = index.SuggestStimulationGroup()
            End If

            Dim matched As New List(Of Integer)()

            For i As Integer = 0 To index.Size - 1
                If [matches](index, i, targetGroup, targetClass, targetType) Then
                    Call matched.Add(i)
                End If
            Next

            If matched.Count = 0 Then
                Throw New InvalidOperationException(
                    $"没有神经元匹配当前的刺激筛选条件 (group='{targetGroup}', class='{targetClass}', primary_type='{targetType}')")
            End If

            Dim selected As Integer()

            If matched.Count > config.StimulationNeurons Then
                ' 使用固定种子做部分洗牌，保证结果可复现
                Dim rng As New Random(config.Seed)

                For i As Integer = 0 To config.StimulationNeurons - 1
                    Dim j As Integer = rng.Next(i, matched.Count)
                    Dim swap As Integer = matched(i)

                    matched(i) = matched(j)
                    matched(j) = swap
                Next

                selected = matched.Take(config.StimulationNeurons).OrderBy(Function(x) x).ToArray
            Else
                selected = matched.ToArray
            End If

            Dim label As String = $"cell type (group='{targetGroup}', class='{targetClass}', type='{targetType}')"

            Return build(config, index, StimulationMode.CellType, selected, label)
        End Function

        ''' <summary>
        ''' 神经元是否匹配筛选条件 (多个条件之间为 AND 关系，空条件表示不筛选)。
        ''' </summary>
        Private Shared Function [matches](index As ConnectomeIndex,
                                          neuron As Integer,
                                          targetGroup As String,
                                          targetClass As String,
                                          targetType As String) As Boolean

            If targetGroup.Length > 0 AndAlso
                Not String.Equals(index.GetGroup(neuron), targetGroup, StringComparison.OrdinalIgnoreCase) Then

                Return False
            End If

            If targetClass.Length > 0 AndAlso
                Not String.Equals(index.GetClass(neuron), targetClass, StringComparison.OrdinalIgnoreCase) Then

                Return False
            End If

            If targetType.Length > 0 AndAlso
                Not String.Equals(index.GetPrimaryType(neuron), targetType, StringComparison.OrdinalIgnoreCase) Then

                Return False
            End If

            Return True
        End Function

        Private Shared Function build(config As SnnConfig,
                                      index As ConnectomeIndex,
                                      mode As StimulationMode,
                                      selected As Integer(),
                                      label As String) As Stimulation

            If selected.Length = 0 Then
                Throw New InvalidOperationException("刺激神经元数量为 0")
            End If

            Dim values As Double() = New Double(selected.Length - 1) {}
            Dim rootIds As Long() = New Long(selected.Length - 1) {}

            For i As Integer = 0 To selected.Length - 1
                values(i) = config.StimulationValue
                rootIds(i) = index.GetRootId(selected(i))
            Next

            Return New Stimulation(mode, selected, values, rootIds, label)
        End Function

        Public Overrides Function ToString() As String
            Return $"{Mode}: {Count} neurons, value={If(Values.Length > 0, Values(0), 0)} ({Label})"
        End Function

    End Class
End Namespace
