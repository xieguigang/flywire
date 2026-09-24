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
