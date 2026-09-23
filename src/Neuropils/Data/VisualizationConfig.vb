Imports FlywireAI.Connectome

Namespace Data

    ''' <summary>
    ''' 三维大脑可视化的配置：在 SNN 仿真的文件布局之上补充可视化所需的表格。
    ''' </summary>
    ''' <remarks>
    ''' 继承 <see cref="SnnConfig"/> 而不是另起一套配置，是为了让"仿真"与"看仿真结果"
    ''' 共用同一个 ``DataDir`` 与同一批表格路径：两个子系统读到的必须是同一份数据。
    ''' </remarks>
    Public Class VisualizationConfig : Inherits SnnConfig

        ''' <summary>
        ''' 标记位置坐标表 (``coordinates.csv``)：``root_id, position, supervoxel_id``。
        ''' </summary>
        ''' <remarks>
        ''' 13.9 MB / 238,909 行，覆盖约 13 万个神经元。文档说明这些坐标是"人工校对时
        ''' 标记的位置"，并不保证是胞体，但它是唯一一张"每神经元一行"的坐标表，
        ''' 因此作为点云的位置来源。
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
        ''' 神经元活动表 (``neuron_activity_&lt;mode&gt;.csv``) 的搜索目录，
        ''' 为空表示 ``&lt;DataDir&gt;\snn-output``。
        ''' </summary>
        ''' <remarks>
        ''' 该文件由 <c>SimulationReport</c> 落盘 (全量 139,255 行逐神经元脉冲计数)。
        ''' 找不到时可以用界面上的"现场运行仿真"按钮重新跑一次 (GPU 全脑 T=30 约几十毫秒)。
        ''' </remarks>
        Public Property ActivityDir As String = ""

        ''' <summary>解析活动表所在目录。</summary>
        Public Function ResolveActivityDir() As String
            If String.IsNullOrWhiteSpace(ActivityDir) Then
                Return System.IO.Path.Combine(DataDir, "snn-output")
            End If

            Return ActivityDir
        End Function

    End Class

End Namespace
