Imports Microsoft.VisualBasic.Imaging.Driver

Namespace Analysis

    ''' <summary>
    ''' 绘图运行时的初始化。
    ''' </summary>
    ''' <remarks>
    ''' <c>PlotEngine</c> 在排版标题 / 坐标轴刻度 / 图例时要量文本高度，而字体度量走的是
    ''' imaging 库里<b>全局注册</b>的文本度量驱动（<c>Font.GetHeight</c> 内部用的是
    ''' <c>DriverLoad.MeasureTextSize</c>，与传入的绘图设备无关）。没有注册时它会直接抛
    ''' <c>InvalidProgramException: missing text size measurement driver!</c>。
    ''' 
    ''' 所以任何要画图表的进程（主窗口、曲线图窗口、命令行出图）都必须先调用
    ''' <see cref="EnsureRegistered"/> —— 它是幂等的。
    ''' </remarks>
    Public NotInheritable Class PlotRuntime

        Private Shared _registered As Boolean
        Private Shared ReadOnly _sync As New Object()

        ''' <summary>注册默认的文本度量 / 光栅化驱动（可重复调用）。</summary>
        Public Shared Sub EnsureRegistered()
            If _registered Then Return

            SyncLock _sync
                If _registered Then Return

                Call ImageDriver.Register()

                _registered = True
            End SyncLock
        End Sub

    End Class

End Namespace
