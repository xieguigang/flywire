Imports System.Text
Imports Microsoft.VisualBasic.Computing.ILCuda.GPUTensor
Imports Microsoft.VisualBasic.Computing.ILCuda.Runtime
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork
Imports tf = Microsoft.VisualBasic.MachineLearning.TensorFlow

Namespace Connectome

    ''' <summary>
    ''' CUDA 后端的应用层注册入口。
    ''' </summary>
    ''' <remarks>
    ''' 分层约定（与 <c>CUDA → TensorFlow → SNN</c> 一致）：
    ''' <c>ILCudaTensor</c> 提供 GPU 实现、<c>TensorFlow</c> 提供可插拔后端契约、
    ''' <c>SNN</c> 只使用契约成员，<b>不知道 CUDA 的存在</b>；
    ''' 因此"注册 GPU"这件事只能由最上层的应用工程（本模块）来完成。
    ''' <para>
    ''' 使用顺序（重要）：
    ''' <list type="number">
    '''   <item>装配网络<b>之前</b>调用 <see cref="TryRegister"/>；</item>
    '''   <item>运行仿真（<c>SparseLIFLayer.ForwardStep</c> 会自动走融合算子 + 设备常驻）；</item>
    '''   <item>结束时调用 <see cref="Unregister"/>（或 <c>ReleaseDeviceBuffers</c>）归还显存。</item>
    ''' </list>
    ''' </para>
    ''' </remarks>
    Public Module GpuRuntime

        Private _registered As Boolean
        Private _lastError As String
        Private _failureDetail As String

        ''' <summary>是否已经成功注册 GPU 后端。</summary>
        Public ReadOnly Property IsRegistered As Boolean
            Get
                Return _registered
            End Get
        End Property

        ''' <summary>最近一次注册失败的原因（成功时为 <c>Nothing</c>）。</summary>
        Public ReadOnly Property LastError As String
            Get
                Return _lastError
            End Get
        End Property

        ''' <summary>当前生效的张量计算后端名称（<c>CUDA</c> / <c>SIMD</c>）。</summary>
        Public ReadOnly Property BackendName As String
            Get
                Return tf.Tensor.computeKernel.Name
            End Get
        End Property

        ''' <summary>CUDA 后端实例（未注册时为 <c>Nothing</c>）。</summary>
        Public ReadOnly Property Backend As CudaTensor
            Get
                Return CudaTensor.Current
            End Get
        End Property

        ''' <summary>
        ''' 当前设备常驻缓冲占用的可读描述（未使用常驻时返回 ``0 B``）。
        ''' </summary>
        Public ReadOnly Property PinnedBytesDescription As String
            Get
                Dim backend As CudaTensor = CudaTensor.Current

                If backend Is Nothing Then
                    Return "0 B"
                End If

                Dim bytes As Long = backend.PinnedDeviceBytes

                If bytes <= 0 Then
                    Return "0 B"
                End If

                Return $"{bytes / 1024.0 / 1024.0:N1} MB"
            End Get
        End Property

        ''' <summary>
        ''' 按配置注册 CUDA 后端；失败时保持 CPU 后端不变并记录原因。
        ''' </summary>
        ''' <param name="config">仿真配置（含设备号、NVRTC 路径、缓存与阈值设置）。</param>
        ''' <param name="reporter">诊断信息回调。</param>
        ''' <returns>注册成功（或此前已注册）返回 <c>True</c>。</returns>
        ''' <remarks>
        ''' 失败是<b>预期内</b>的路径而不是异常：没有 NVIDIA 显卡、NVRTC 缺失、
        ''' 驱动版本不支持工具包（PTX ISA 被拒）都会走到这里，此时仿真继续在 CPU 上运行，
        ''' 只是慢一些。因此这里返回布尔并打印可读的失败原因，
        ''' 而不是抛异常中断整条业务流程。
        ''' </remarks>
        Public Function TryRegister(config As SnnConfig, Optional reporter As Action(Of String) = Nothing) As Boolean
            If config Is Nothing Then Throw New ArgumentNullException(NameOf(config))

            If Not config.UseGpu Then
                _registered = False
                Call report(reporter, "GPU 未启用（配置 UseGpu=False），使用 SIMD CPU 后端")

                Return False
            End If

            ' 已注册过：只把后端切回来（避免重复创建引擎与显存上下文）
            If _registered AndAlso CudaTensor.Current IsNot Nothing Then
                tf.Tensor.computeKernel = CudaTensor.Current
                Call report(reporter, $"复用已注册的 CUDA 后端：{CudaTensor.Current.DescribeDevice()}")

                Return True
            End If

            Dim options As New EngineOptions With {
                .DeviceOrdinal = config.GpuDeviceOrdinal,
                .NvrtcPath = config.GpuNvrtcPath
            }

            Call report(reporter, $"正在注册 CUDA 后端（device={config.GpuDeviceOrdinal}，nvrtc={If(config.GpuNvrtcPath, "<auto>")}）...")

            If Not CudaTensor.Register(options, cacheBytes:=config.GpuCacheBytes, useFp32Gemm:=True) Then
                _registered = False
                _lastError = CudaTensor.LastError

                Dim sb As New StringBuilder()

                If Not String.IsNullOrWhiteSpace(_lastError) Then
                    Call sb.AppendLine($"  error: {_lastError}")
                End If

                For Each line As String In options.Diagnostics
                    Call sb.AppendLine($"  {line}")
                Next

                _failureDetail = sb.ToString.TrimEnd()

                Call report(reporter, $"CUDA 后端注册失败，已回退 CPU：{Environment.NewLine}{_failureDetail}")

                Return False
            End If

            Dim backend As CudaTensor = CudaTensor.Current

            ' 稀疏 SpMM 的规模门槛：全脑 nnz 远超默认值，这里只在显式配置时覆盖
            If config.GpuMinSparseNnz > 0 Then
                CudaTensor.MinSparseNnz = config.GpuMinSparseNnz
            End If

            _registered = True
            _lastError = Nothing
            _failureDetail = Nothing

            Call report(reporter, $"CUDA 后端已就绪：{backend.DescribeDevice()}")
            Call report(reporter, $"内核可用性：{backend.DescribeKernels()}")
            Call report(reporter, $"融合 LIF 单步内核可用：{backend.IsFusedLifAvailable}")

            Return True
        End Function

        ''' <summary>
        ''' 把计算后端切回 SIMD CPU 实现。
        ''' </summary>
        ''' <remarks>
        ''' 之后应当对每个 <see cref="SparseLIFLayer"/> 调用 <c>ReleaseDeviceBuffers</c>
        ''' （或者重新 <c>ResetState</c>）归还常驻显存 —— 常驻缓冲不参与 LRU 淘汰，
        ''' 不释放就会一直占到后端被回收为止。
        ''' </remarks>
        Public Sub Unregister()
            CudaTensor.Unregister()
            _registered = False
        End Sub

        ''' <summary>
        ''' 后端与设备概览（写入仿真报告与基准 csv）。
        ''' </summary>
        Public Function Describe() As String
            Dim sb As New StringBuilder()

            Call sb.AppendLine($"backend         : {BackendName}")
            Call sb.AppendLine($"use gpu         : {IsRegistered}")

            If IsRegistered AndAlso CudaTensor.Current IsNot Nothing Then
                Dim backend As CudaTensor = CudaTensor.Current

                Call sb.AppendLine($"device          : {backend.DescribeDevice()}")
                Call sb.AppendLine($"fused lif ok    : {backend.IsFusedLifAvailable}")
                Call sb.AppendLine($"sparse spmm ok  : {backend.IsSparseSpmmAvailable}")
                Call sb.AppendLine($"pinned bytes    : {backend.PinnedDeviceBytes:N0}")
                Call sb.AppendLine($"kernels         : {backend.DescribeKernels()}")
            ElseIf Not String.IsNullOrWhiteSpace(_failureDetail) Then
                Call sb.AppendLine($"fallback reason : {_failureDetail}")
            End If

            Return sb.ToString
        End Function

        Private Sub report(reporter As Action(Of String), message As String)
            If reporter Is Nothing Then
                Return
            End If

            Call reporter(message)
        End Sub

    End Module

End Namespace
