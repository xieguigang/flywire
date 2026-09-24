Imports System.Text

''' <summary>
''' 关于 Neuropils 的独立窗口：把原 <see cref="PageFlywireCanvas"/> 的 onAbout 里的说明文本
''' 抽到这里，显示在一个只读多行的 <see cref="TextBox"/> 里（而非 MessageBox）。
''' </summary>
''' <remarks>
''' 控件布局在 <c>AboutForm.Designer.vb</c> 的 <c>InitializeComponent</c> 中声明式建好，
''' 文本本身在 <see cref="AboutForm_Load"/> 里灌入；弹出用 <see cref="ShowAbout"/>。
''' </remarks>
Public Class AboutForm

    ''' <summary>载入时把说明文本灌入只读 TextBox（文本与原 onAbout 完全一致）。</summary>
    Private Sub AboutForm_Load(sender As Object, e As EventArgs) Handles Me.Load
        m_textBox.Text = buildAboutText()
        m_textBox.SelectionStart = 0
        m_textBox.ScrollToCaret()
    End Sub

    ''' <summary>
    ''' 原 PageFlywireCanvas.onAbout 里的说明文本（原样搬过来，未作改动）。
    ''' </summary>
    Private Shared Function buildAboutText() As String
        Dim sb As New StringBuilder()

        Call sb.AppendLine("数据来源: FlyWire FAFB v783 (codex.flywire.ai)")
        Call sb.AppendLine("神经元 139,255 / 连接 534 万条 (≥5 突触)")
        Call sb.AppendLine("点云位置来自 coordinates.csv，脑区归属由 neuropil_synapse_table.csv 取 argmax")
        Call sb.AppendLine("渲染: Microsoft.VisualBasic.Drawing (Direct3D 11)")
        Call sb.AppendLine()
        Call sb.AppendLine("快捷键: 左键旋转 / 右键平移 / 滚轮缩放")
        Call sb.AppendLine("        R 重置视角 / F 适配视图 / G 地面 / C 连线 / S 截图")
        Call sb.AppendLine()
        Call sb.AppendLine("电刺激仿真:")
        Call sb.AppendLine("  1. 勾选工具条上的「电刺激模式」")
        Call sb.AppendLine("  2. 在神经元上按住左键并松开（按住越久注入电流越强，按住期间会实时回显强度）")
        Call sb.AppendLine("  3. 松开后自动运行一次全脑 SNN 仿真，随后在右侧面板回放激活过程")
        Call sb.AppendLine("     （被激活的神经元会点亮并放大，前几步保留余辉）")
        Call sb.AppendLine("  结果落盘到 <数据目录>\snn-output\<时间戳>_stimulation\")
        Call sb.AppendLine()
        Call sb.AppendLine("响应曲线:")
        Call sb.AppendLine("  点状态栏右下角的「响应曲线」链接，或直接点这里")
        Call sb.AppendLine("  横轴 = 时间步，纵轴 = 神经元的响应电信号强度（默认取触发前膜电位）")
        Call sb.AppendLine("  可按 主导脑区 / 神经递质 / 细胞类型 / 分类层级 勾选神经元集合，")
        Call sb.AppendLine("  按「逐个神经元 / 组均值 / 组均值 ± 包络」三种方式绘制")
        Call sb.AppendLine()
        Call sb.AppendLine("命令行:")
        Call sb.AppendLine("  Neuropils.exe [数据目录]")
        Call sb.AppendLine("  Neuropils.exe --selftest <报告.txt> [数据目录]")
        Call sb.AppendLine("      数据链路自检 (读表/着色/装配/拾取) 并给出实测耗时")
        Call sb.AppendLine("  Neuropils.exe --snapshot <图片.png> [数据目录] [着色 0..4] [连接 0..3]")
        Call sb.AppendLine("      载入后离屏抓一帧写盘然后退出 (0=不画 1=逐条 2=宏连接 3=选中神经元)")
        Call sb.AppendLine("  Neuropils.exe --stimulate <报告.txt> [数据目录] [神经元] [强度列表,默认 1,2,4,8,16]")
        Call sb.AppendLine("      对单个神经元扫强度跑仿真，输出逐步激活规模并落盘回放数据")

        Return sb.ToString()
    End Function

    ''' <summary>ESC 关闭窗口（确定按钮已绑定 DialogResult.OK，回车也会触发）。</summary>
    Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
        If keyData = Keys.Escape Then
            Call Close()
            Return True
        End If

        Return MyBase.ProcessDialogKey(keyData)
    End Function

    ''' <summary>
    ''' 以模态方式弹出关于窗口（owner 居中）。
    ''' </summary>
    ''' <param name="owner">父窗口（通常为 <see cref="FormMain"/> 或 <see cref="PageFlywireCanvas"/>）</param>
    Public Shared Sub ShowAbout(owner As IWin32Window)
        Using f As New AboutForm()
            Call f.ShowDialog(owner)
        End Using
    End Sub

End Class
