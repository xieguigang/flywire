Imports Galaxy.Workbench.DockDocument
Imports Microsoft.VisualBasic.Drawing.DirectX
Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class PageFlywireCanvas
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


    ' ---- 工具条（声明式布局，参照 ResponseChartForm.Designer.vb 的模式）----
    ' 被其它类 (StimulationExperiment) 通过 FormMain 实例访问的保持 Friend；
    ' 事件源用 WithEvents 以便 Handles 绑定，替代原 createToolbar 里的 AddHandler。
    Dim m_toolStrip As ToolStrip
    Private WithEvents m_dimensionBox As ToolStripComboBox
    Private WithEvents m_connectionBox As ToolStripComboBox
    Private WithEvents m_lineColorBox As ToolStripComboBox
    Private WithEvents m_renderModeBox As ToolStripComboBox

    Friend m_holdLabel As ToolStripLabel

    ' ---- 主界面布局（声明式，参照 ResponseChartForm.Designer.vb 的模式）----
    ' 事件源用 WithEvents 以便 Handles 绑定，替代原 initializeUi / createSidebar / createReplayPanel 里的 AddHandler。
    Friend WithEvents m_canvas As DxScene3DCanvas

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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(PageFlywireCanvas))
        m_canvas = New DxScene3DCanvas()
        m_toolStrip = New ToolStrip()
        colorLabel = New ToolStripLabel()
        m_dimensionBox = New ToolStripComboBox()
        sep111 = New ToolStripSeparator()
        modeLabel = New ToolStripLabel()
        m_renderModeBox = New ToolStripComboBox()
        connLabel = New ToolStripLabel()
        m_connectionBox = New ToolStripComboBox()
        lineColorLabel = New ToolStripLabel()
        m_lineColorBox = New ToolStripComboBox()
        m_holdLabel = New ToolStripLabel()
        m_toolStrip.SuspendLayout()
        SuspendLayout()
        ' 
        ' m_canvas
        ' 
        m_canvas.AutoClear = False
        m_canvas.BackColor = Color.Black
        m_canvas.BackgroundColor = Color.FromArgb(CByte(12), CByte(12), CByte(18))
        m_canvas.Dock = DockStyle.Fill
        m_canvas.Location = New Point(0, 25)
        m_canvas.Name = "m_canvas"
        m_canvas.RenderMode = SceneRenderMode.PointCloud
        m_canvas.ShowConnections = False
        m_canvas.ShowGround = False
        m_canvas.Size = New Size(1500, 875)
        m_canvas.TabIndex = 0
        m_canvas.UseEmbeddedColor = True
        ' 
        ' m_toolStrip
        ' 
        m_toolStrip.GripStyle = ToolStripGripStyle.Hidden
        m_toolStrip.Items.AddRange(New ToolStripItem() {colorLabel, m_dimensionBox, sep111, modeLabel, m_renderModeBox, connLabel, m_connectionBox, lineColorLabel, m_lineColorBox, m_holdLabel})
        m_toolStrip.Location = New Point(0, 0)
        m_toolStrip.Name = "m_toolStrip"
        m_toolStrip.Size = New Size(1500, 25)
        m_toolStrip.TabIndex = 2
        ' 
        ' colorLabel
        ' 
        colorLabel.Name = "colorLabel"
        colorLabel.Size = New Size(85, 22)
        colorLabel.Text = "神经元着色："
        ' 
        ' m_dimensionBox
        ' 
        m_dimensionBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_dimensionBox.Items.AddRange(New Object() {"主导脑区 (neuropil)", "神经递质", "细胞类型", "分类层级", "仿真活跃度"})
        m_dimensionBox.Name = "m_dimensionBox"
        m_dimensionBox.Size = New Size(150, 25)
        ' 
        ' sep111
        ' 
        sep111.Name = "sep111"
        sep111.Size = New Size(6, 25)
        ' 
        ' modeLabel
        ' 
        modeLabel.Name = "modeLabel"
        modeLabel.Size = New Size(46, 22)
        modeLabel.Text = "模式："
        ' 
        ' m_renderModeBox
        ' 
        m_renderModeBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_renderModeBox.Items.AddRange(New Object() {"点云", "线框", "实体"})
        m_renderModeBox.Name = "m_renderModeBox"
        m_renderModeBox.Size = New Size(110, 25)
        ' 
        ' connLabel
        ' 
        connLabel.Name = "connLabel"
        connLabel.Size = New Size(46, 22)
        connLabel.Text = "连接："
        ' 
        ' m_connectionBox
        ' 
        m_connectionBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_connectionBox.Items.AddRange(New Object() {"连接 (逐条)", "脑区宏连接", "选中神经元的连接"})
        m_connectionBox.Name = "m_connectionBox"
        m_connectionBox.Size = New Size(150, 25)
        ' 
        ' lineColorLabel
        ' 
        lineColorLabel.Name = "lineColorLabel"
        lineColorLabel.Size = New Size(111, 22)
        lineColorLabel.Text = "神经元链接颜色："
        ' 
        ' m_lineColorBox
        ' 
        m_lineColorBox.DropDownStyle = ComboBoxStyle.DropDownList
        m_lineColorBox.Items.AddRange(New Object() {"连线: 前突触颜色", "连线: 递质类型", "连线: 单色"})
        m_lineColorBox.Name = "m_lineColorBox"
        m_lineColorBox.Size = New Size(130, 25)
        ' 
        ' m_holdLabel
        ' 
        m_holdLabel.Name = "m_holdLabel"
        m_holdLabel.Size = New Size(0, 22)
        ' 
        ' PageFlywireCanvas
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1500, 900)
        Controls.Add(m_canvas)
        Controls.Add(m_toolStrip)
        DockAreas = Microsoft.VisualStudio.WinForms.Docking.DockAreas.Float Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockLeft Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockRight Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockTop Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.DockBottom Or Microsoft.VisualStudio.WinForms.Docking.DockAreas.Document
        DoubleBuffered = True
        Icon = CType(resources.GetObject("$this.Icon"), Icon)
        MinimumSize = New Size(900, 600)
        Name = "PageFlywireCanvas"
        ShowHint = Microsoft.VisualStudio.WinForms.Docking.DockState.Unknown
        StartPosition = FormStartPosition.CenterScreen
        TabPageContextMenuStrip = DockContextMenuStrip1
        Text = "Neuropils - Drosophila brain 3D viewer"
        m_toolStrip.ResumeLayout(False)
        m_toolStrip.PerformLayout()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Dim colorLabel As ToolStripLabel
    Dim modeLabel As ToolStripLabel
    Dim connLabel As ToolStripLabel
    Dim lineColorLabel As ToolStripLabel
    Dim sep111 As ToolStripSeparator

End Class
