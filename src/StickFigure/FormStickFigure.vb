Imports System.Drawing
Imports System.Windows.Forms

''' <summary>
''' 主窗体：只承载 <see cref="FigureCanvas"/>，全部 UI 在组合控件里构建。
''' </summary>
Public Class FormStickFigure

    Public Sub New()
        InitializeComponent()

        Text = "3D 火柴人物理仿真 Demo - StickFigure"
        BackColor = Color.FromArgb(15, 20, 28)
        ForeColor = Color.FromArgb(232, 238, 246)
        Font = New Font("Segoe UI", 9.0F)
        MinimumSize = New Size(1024, 680)
        StartPosition = FormStartPosition.CenterScreen
    End Sub

End Class
