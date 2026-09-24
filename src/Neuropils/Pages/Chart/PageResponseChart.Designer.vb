Imports Galaxy.Workbench.DockDocument
Imports Microsoft.VisualBasic.Drawing.DirectX

' ResponseChartForm 的界面布局（Windows 窗体设计器维护的声明式代码）。
' 这里只放"控件怎么摆"的代码：控件实例化、属性赋值、容器装配与事件挂接；
' 数据装配与绘制逻辑 (refreshCategories / rebuildSeries / onRender 等) 仍然留在 ResponseChartForm.vb 里。
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class PageResponseChart
    Inherits DocumentWindow

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(PageResponseChart))
        m_canvas = New DxCanvas()
        SuspendLayout()
        ' 
        ' m_canvas
        ' 
        m_canvas.BackColor = Color.LightSkyBlue
        m_canvas.Dock = DockStyle.Fill
        m_canvas.Location = New Point(0, 0)
        m_canvas.Name = "m_canvas"
        m_canvas.Size = New Size(1270, 764)
        m_canvas.TabIndex = 0
        ' 
        ' PageResponseChart
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        BackColor = Color.FromArgb(CByte(21), CByte(29), CByte(38))
        ClientSize = New Size(1270, 764)
        Controls.Add(m_canvas)
        DockAreas = Microsoft.VisualStudio.WinForms.Docking.DockAreas.Float Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockLeft Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockRight Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockTop Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockBottom Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.Document
        DoubleBuffered = True
        Font = New Font("Segoe UI", 9F)
        ForeColor = Color.FromArgb(CByte(226), CByte(232), CByte(240))
        Icon = CType(resources.GetObject("$this.Icon"), Icon)
        MinimumSize = New Size(820, 520)
        Name = "PageResponseChart"
        ShowHint = Microsoft.VisualStudio.WinForms.Docking.DockState.Unknown
        StartPosition = FormStartPosition.CenterParent
        TabPageContextMenuStrip = DockContextMenuStrip1
        Text = "电刺激响应曲线"
        ResumeLayout(False)
    End Sub

    Private WithEvents m_canvas As DxCanvas
End Class