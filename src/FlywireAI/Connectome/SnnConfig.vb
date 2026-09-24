Imports System.Text
Imports Microsoft.VisualBasic.DeepLearning.SpikingNeuralNetwork

Namespace Connectome

    ''' <summary>
    ''' 外部驱动模式。
    ''' </summary>
    Public Enum StimulationMode

        ''' <summary>随机挑选一批神经元进行刺激。</summary>
        RandomNeurons = 0

        ''' <summary>按照 group / class / primary_type 筛选神经元进行刺激。</summary>
        CellType = 1

    End Enum

    ''' <summary>
    ''' 果蝇全脑 SNN 仿真的配置对象：数据文件、网络参数、刺激方式与输出目录。
    ''' </summary>
    Public Class SnnConfig

#Region "数据文件"

        ''' <summary>FAFB v783 数据文件所在的文件夹。</summary>
        Public Property DataDir As String = "F:\flywire\FAFB-v783"

        ''' <summary>突触连接表 (5,342,446 行，已过滤 &lt;5 突触)。</summary>
        Public Property ConnectionsCsv As String = "connections_princeton.csv"

        Public Property NamesCsv As String = "names.csv"
        Public Property ClassificationCsv As String = "classification.csv"
        Public Property CellTypesCsv As String = "consolidated_cell_types.csv"
        Public Property NeuronsCsv As String = "neurons.csv"

        ''' <summary>
        ''' 标记位置坐标表 (``coordinates.csv``)：``root_id, position, supervoxel_id``。
        ''' </summary>
        ''' <remarks>
        ''' 13.9 MB / 238,909 行，覆盖约 13 万个神经元。文档说明这些坐标是"人工校对时
        ''' 标记的位置"，并不保证是胞体，但它是唯一一张"每神经元一行"的坐标表，
        ''' 因此作为点云的位置来源。
        ''' 
        ''' 放在这里（而不是只在可视化配置里）的原因：<see cref="FAFBv783.FafbMsgPackStorage"/>
        ''' 需要把<b>全部</b>数据源文件转储成 msgpack，它只能看到 <see cref="SnnConfig"/>。
        ''' </remarks>
        Public Property CoordinatesCsv As String = "coordinates.csv"

        ''' <summary>
        ''' 按脑区统计的突触表 (``neuropil_synapse_table.csv``)：321 列 / 134,181 行。
        ''' </summary>
        ''' <remarks>
        ''' 每行是一个神经元对 80 个脑区的输入/输出突触数与伙伴数。对每个神经元取
        ''' "输入+输出突触数最多的脑区"即可得到它的主导脑区，这是"不同脑区不同颜色"
        ''' 的依据 (数据集里没有直接给出每个神经元的脑区归属)。
        ''' </remarks>
        Public Property NeuropilTableCsv As String = "neuropil_synapse_table.csv"

        ''' <summary>
        ''' msgpack 转储包的<b>显式路径</b>（空串 = 按 <see cref="FafbMsgPackStorage.ResolvePackFile"/>
        ''' 的候选顺序自动查找）。
        ''' </summary>
        ''' <remarks>
        ''' 界面上"打开模型包"的按钮选中文件之后写到这里，之后主界面与贪吃蛇等
        ''' 所有数据消费方都会用这一份。
        ''' </remarks>
        Public Property PackFile As String = ""

#End Region

#Region "网络与仿真参数"

        ''' <summary>仿真时间步数 T。</summary>
        Public Property TimeSteps As Integer = 30

        ''' <summary>LIF 膜电位的泄漏系数 β (取值于 0~1 之间)。</summary>
        Public Property Beta As Double = 0.9

        ''' <summary>LIF 发放阈值 θ。</summary>
        Public Property Threshold As Double = 1.0

        Public Property ResetMode As LIFResetMode = LIFResetMode.ZeroOnSpike

        ''' <summary>
        ''' 输入编码方式。默认使用 <see cref="SpikeEncoding.DirectCurrent"/>：
        ''' 每步注入同一份恒流，仿真结果确定可复现 (RateCoding 为伯努利采样，含编码噪声)。
        ''' </summary>
        Public Property Encoding As SpikeEncoding = SpikeEncoding.DirectCurrent

        ''' <summary>兴奋性突触的极性增益 (用于调整 E/I 比例)。</summary>
        Public Property ExcitatoryGain As Double = 1.0

        ''' <summary>抑制性突触的极性增益 (用于调整 E/I 比例)。</summary>
        Public Property InhibitoryGain As Double = 1.0

        ''' <summary>全局权重增益；``&lt;= 0`` 表示由活动标定自动决定。</summary>
        Public Property GlobalGain As Double = 0

        ''' <summary>随机种子 (网络编码与刺激神经元抽样共用)。</summary>
        Public Property Seed As Integer = 42

        ''' <summary>仿真过程中每多少个时间步打印一次进度。</summary>
        Public Property ProgressEverySteps As Integer = 5

#End Region

#Region "活动标定"

        ''' <summary>标定探针的仿真步数 (短于正式仿真)。</summary>
        Public Property CalibrationProbeSteps As Integer = 10

        ''' <summary>标定候选的全局增益列表。</summary>
        Public Property CalibrationCandidates As Double() = {1.0, 2.0, 4.0, 8.0}

        ''' <summary>标定所期望的活跃神经元比例。</summary>
        Public Property TargetActiveFraction As Double = 0.05

        ''' <summary>活跃比例的下限 (用于判定标定是否落在合理区间)。</summary>
        Public Property MinActiveFraction As Double = 0.01

        ''' <summary>活跃比例的上限 (用于判定标定是否落在合理区间)。</summary>
        Public Property MaxActiveFraction As Double = 0.3

#End Region

#Region "刺激"

        ''' <summary>外部驱动模式。</summary>
        Public Property Mode As StimulationMode = StimulationMode.RandomNeurons

        ''' <summary>刺激神经元数量 (inputSize)。</summary>
        Public Property StimulationNeurons As Integer = 5000

        ''' <summary>刺激强度 (输入张量取值，约定 [0,1])。</summary>
        Public Property StimulationValue As Double = 0.9

        ''' <summary>CellType 模式下的 group 筛选条件 (空串表示不筛选)。</summary>
        Public Property TargetGroup As String = ""

        ''' <summary>CellType 模式下的 class 筛选条件 (空串表示不筛选)。</summary>
        Public Property TargetClass As String = ""

        ''' <summary>CellType 模式下的 primary_type 筛选条件 (空串表示不筛选)。</summary>
        Public Property TargetPrimaryType As String = ""

#End Region

#Region "GPU 与精度档位"

        ''' <summary>
        ''' 是否启用 CUDA GPU 张量后端。
        ''' </summary>
        ''' <remarks>
        ''' 默认关闭：CPU 路径是确定性的基准，便于与 GPU 结果对拍。
        ''' 注册失败（无 NVIDIA 显卡 / NVRTC 不可用 / 驱动不匹配）时<b>自动回退 CPU</b>，
        ''' 失败原因可通过 <see cref="GpuRuntime.LastError"/> 与 <see cref="GpuRuntime.Describe"/> 查看。
        ''' </remarks>
        Public Property UseGpu As Boolean = False

        ''' <summary>GPU 设备序号。</summary>
        Public Property GpuDeviceOrdinal As Integer = 0

        ''' <summary>
        ''' 显式指定 ``nvrtc64_*.dll`` 的路径；``Nothing`` 表示自动搜索
        ''' （``CUDA_PATH`` → ``%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA\*\bin\x64`` → PATH → 程序目录）。
        ''' </summary>
        Public Property GpuNvrtcPath As String = Nothing

        ''' <summary>设备驻留缓存上限 (字节)；``0`` 表示按可用显存自适应。</summary>
        Public Property GpuCacheBytes As Long = 0

        ''' <summary>
        ''' 稀疏 SpMM 走 GPU 的最小非零数；``0`` 表示使用后端默认值（65,536）。
        ''' </summary>
        ''' <remarks>
        ''' 全脑规模（nnz ≈ 373 万）远超默认阈值。调小它只对"小规模对拍"有意义：
        ''' 阈值未通过时后端会静默回退 CPU，从而出现"以为在测 GPU、其实跑的是 CPU"。
        ''' </remarks>
        Public Property GpuMinSparseNnz As Integer = 0

        ''' <summary>
        ''' 设备常驻状态的精度档位。
        ''' </summary>
        ''' <remarks>
        ''' <see cref="LifResidentPrecision.Double64"/>（默认）下 GPU 与 CPU <b>逐位一致</b>，
        ''' 是"对拍验收"必须的档位；<see cref="LifResidentPrecision.Single32"/> 是最快档，
        ''' 膜电位降为单精度，可能在阈值边界上少发/多发个别脉冲（脉冲计数会出现整数差异）。
        ''' </remarks>
        Public Property ResidentPrecision As LifResidentPrecision = LifResidentPrecision.Double64

        ''' <summary>
        ''' 是否记录逐步脉冲轨迹。
        ''' </summary>
        ''' <remarks>
        ''' ``True``（默认）：每步回读一次脉冲张量，报告里可以给出逐步活跃曲线（与既有报告一致）。
        ''' ``False``：只维护设备端计数累加器，整段仿真<b>零逐步回读</b>、逐步统计留空 —— 最快档。
        ''' </remarks>
        Public Property KeepHistory As Boolean = True

        ''' <summary>
        ''' 是否优先使用融合单步算子（``LifStep``）。
        ''' </summary>
        ''' <remarks>
        ''' 置为 ``False`` 会退回逐算子路径，唯一好处是能拿到膜电位轨迹（<c>UHistory</c>），
        ''' 代价是每步多分配 5 个中间张量、GPU 下每步多若干次显存往返。
        ''' </remarks>
        Public Property UseFusedStep As Boolean = True

#End Region

#Region "输出"

        ''' <summary>结果输出目录；为空的时候默认为 ``&lt;DataDir&gt;\snn-output\&lt;时间戳&gt;``。</summary>
        Public Property OutputDir As String = ""

        ''' <summary>报告之中 top 放电神经元的数量。</summary>
        Public Property TopNeurons As Integer = 50

#End Region

        ''' <summary>
        ''' 解析数据文件的完整路径。
        ''' </summary>
        Public Function ResolvePath(fileName As String) As String
            Return System.IO.Path.Combine(DataDir, fileName)
        End Function

        ''' <summary>
        ''' 取得结果输出目录 (自动创建时间戳子目录)。
        ''' </summary>
        Public Function GetOutputDir() As String
            If String.IsNullOrWhiteSpace(OutputDir) Then
                Return System.IO.Path.Combine(DataDir, "snn-output", DateTime.Now.ToString("yyyyMMdd_HHmmss"))
            Else
                Return OutputDir
            End If
        End Function

        ''' <summary>
        ''' 配置的合法性检查。
        ''' </summary>
        Public Function Validate() As String()
            Dim issues As New List(Of String)

            If TimeSteps <= 0 Then
                issues.Add("TimeSteps must be a positive integer")
            End If
            If Beta <= 0 OrElse Beta >= 1 Then
                issues.Add($"Beta({Beta}) must be inside the open interval (0, 1)")
            End If
            If Threshold <= 0 Then
                issues.Add("Threshold must be positive")
            End If
            If ExcitatoryGain < 0 OrElse InhibitoryGain < 0 Then
                issues.Add("E/I polarity gains must not be negative")
            End If
            If StimulationNeurons <= 0 Then
                issues.Add("StimulationNeurons must be a positive integer")
            End If
            If StimulationValue < 0 OrElse StimulationValue > 1 Then
                issues.Add($"StimulationValue({StimulationValue}) must be inside [0, 1]")
            End If
            If CalibrationProbeSteps <= 0 Then
                issues.Add("CalibrationProbeSteps must be a positive integer")
            End If

            Return issues.ToArray
        End Function

        ''' <summary>
        ''' 配置摘要 (控制台报告与 simulation_summary.csv 使用)。
        ''' </summary>
        Public Function Describe() As String
            Dim sb As New StringBuilder()

            Call sb.AppendLine($"data dir           : {DataDir}")
            Call sb.AppendLine($"connections table  : {ConnectionsCsv}")
            Call sb.AppendLine($"time steps (T)     : {TimeSteps}")
            Call sb.AppendLine($"beta / threshold   : {Beta} / {Threshold}")
            Call sb.AppendLine($"input encoding     : {Encoding}")
            Call sb.AppendLine($"E/I polarity gain  : {ExcitatoryGain} / {InhibitoryGain}")
            Call sb.AppendLine($"global gain        : {If(GlobalGain > 0, GlobalGain.ToString, "<auto calibration>")}")
            Call sb.AppendLine($"seed               : {Seed}")
            Call sb.AppendLine($"compute backend    : {If(UseGpu, GpuRuntime.BackendName, "SIMD (CPU)")}")
            Call sb.AppendLine($"gpu device ordinal : {GpuDeviceOrdinal}")
            Call sb.AppendLine($"fused lif step     : {UseFusedStep} (precision={ResidentPrecision}, keepHistory={KeepHistory})")
            Call sb.AppendLine($"stimulation mode   : {Mode}")
            Call sb.AppendLine($"stimulated neurons : {StimulationNeurons} (value={StimulationValue})")
            Call sb.AppendLine($"target group       : {TargetGroup}")
            Call sb.AppendLine($"target class       : {TargetClass}")
            Call sb.AppendLine($"target primary type: {TargetPrimaryType}")
            Call sb.AppendLine($"output dir         : {GetOutputDir()}")

            Return sb.ToString
        End Function

        ''' <summary>
        ''' 配置摘要 (单行形式，用于 csv 的 kv 行输出)。
        ''' </summary>
        Public Function ToKeyValues() As NamedValues
            Return New NamedValues() _
                .Add("data_dir", DataDir) _
                .Add("connections_csv", ConnectionsCsv) _
                .Add("time_steps", TimeSteps) _
                .Add("beta", Beta) _
                .Add("threshold", Threshold) _
                .Add("encoding", Encoding.ToString) _
                .Add("excitatory_gain", ExcitatoryGain) _
                .Add("inhibitory_gain", InhibitoryGain) _
                .Add("global_gain", GlobalGain) _
                .Add("seed", Seed) _
                .Add("stimulation_mode", Mode.ToString) _
                .Add("stimulation_neurons", StimulationNeurons) _
                .Add("stimulation_value", StimulationValue) _
                .Add("target_group", TargetGroup) _
                .Add("target_class", TargetClass) _
                .Add("target_primary_type", TargetPrimaryType) _
                .Add("use_gpu", UseGpu) _
                .Add("compute_backend", If(UseGpu, GpuRuntime.BackendName, "SIMD")) _
                .Add("use_fused_lif_step", UseFusedStep) _
                .Add("resident_precision", ResidentPrecision.ToString) _
                .Add("keep_history", KeepHistory)
        End Function

    End Class

    ''' <summary>
    ''' 简单的有序 key/value 集合 (保持写入 csv 时的字段顺序)。
    ''' </summary>
    Public Class NamedValues : Inherits List(Of KeyValuePair(Of String, String))

        Public Overloads Function Add(key As String, value As Object) As NamedValues
            Call MyBase.Add(New KeyValuePair(Of String, String)(key, If(value Is Nothing, "", value.ToString)))

            Return Me
        End Function
    End Class

End Namespace
