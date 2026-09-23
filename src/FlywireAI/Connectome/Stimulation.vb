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
