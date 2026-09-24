<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class FormMain
    Inherits System.Windows.Forms.Form

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

    ' ---- 顶部菜单栏（声明式布局，参照 ResponseChartForm.Designer.vb 的模式）----
    ' 所有控件对象都在 InitializeComponent 中实例化并赋常数字面量；
    ' 事件绑定改由 FormMain.vb 里对应的 Handles 子句完成。
    Dim m_menuStrip As MenuStrip
    Dim m_fileMenu As ToolStripMenuItem
    Dim m_viewMenu As ToolStripMenuItem
    Dim m_helpMenu As ToolStripMenuItem
    Dim m_separator As ToolStripSeparator
    Dim WithEvents m_openItem As ToolStripMenuItem
    Dim WithEvents m_snapshotItem As ToolStripMenuItem
    Dim WithEvents m_exitItem As ToolStripMenuItem
    Dim WithEvents m_resetItem As ToolStripMenuItem
    Dim WithEvents m_aboutItem As ToolStripMenuItem

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        SuspendLayout()
        ' 
        ' FormMain
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1411, 818)
        Name = "FormMain"
        Text = "Neuropils"
        ' 
        ' m_menuStrip
        ' 顶部菜单栏：停靠在窗体顶端，宽度取窗体客户区宽度 (1500, 见 initializeUi 的 ClientSize)，高度 24 为单排菜单的标准高度
        ' 
        m_menuStrip = New MenuStrip()
        m_menuStrip.Dock = DockStyle.Top
        m_menuStrip.Location = New Point(0, 0)
        m_menuStrip.Name = "m_menuStrip"
        m_menuStrip.Size = New Size(1500, 24)
        m_menuStrip.TabIndex = 0
        m_menuStrip.Text = ""
        ' 
        ' m_fileMenu
        ' 
        m_fileMenu = New ToolStripMenuItem()
        m_fileMenu.Name = "m_fileMenu"
        m_fileMenu.Size = New Size(61, 21)
        m_fileMenu.Text = "文件 (&F)"
        ' 
        ' m_openItem
        ' 
        m_openItem = New ToolStripMenuItem()
        m_openItem.Name = "m_openItem"
        m_openItem.Size = New Size(180, 22)
        m_openItem.Text = "打开数据目录 (&O)..."
        ' 
        ' m_snapshotItem
        ' 
        m_snapshotItem = New ToolStripMenuItem()
        m_snapshotItem.Name = "m_snapshotItem"
        m_snapshotItem.Size = New Size(168, 22)
        m_snapshotItem.Text = "保存截图 (&S)..."
        ' 
        ' m_separator
        ' 
        m_separator = New ToolStripSeparator()
        m_separator.Name = "m_separator"
        m_separator.Size = New Size(177, 6)
        ' 
        ' m_exitItem
        ' 
        m_exitItem = New ToolStripMenuItem()
        m_exitItem.Name = "m_exitItem"
        m_exitItem.Size = New Size(120, 22)
        m_exitItem.Text = "退出 (&X)"
        ' 
        ' m_viewMenu
        ' 
        m_viewMenu = New ToolStripMenuItem()
        m_viewMenu.Name = "m_viewMenu"
        m_viewMenu.Size = New Size(60, 21)
        m_viewMenu.Text = "视图 (&V)"
        ' 
        ' m_resetItem
        ' 
        m_resetItem = New ToolStripMenuItem()
        m_resetItem.Name = "m_resetItem"
        m_resetItem.Size = New Size(140, 22)
        m_resetItem.Text = "重置视角 (&R)"
        ' 
        ' m_helpMenu
        ' 
        m_helpMenu = New ToolStripMenuItem()
        m_helpMenu.Name = "m_helpMenu"
        m_helpMenu.Size = New Size(60, 21)
        m_helpMenu.Text = "帮助 (&H)"
        ' 
        ' m_aboutItem
        ' 
        m_aboutItem = New ToolStripMenuItem()
        m_aboutItem.Name = "m_aboutItem"
        m_aboutItem.Size = New Size(160, 22)
        m_aboutItem.Text = "关于数据来源 (&A)"
        ' 
        ' m_menuStrip 容器装配
        ' 
        m_menuStrip.SuspendLayout()
        m_fileMenu.DropDownItems.AddRange(New ToolStripItem() {m_openItem, m_snapshotItem, m_separator, m_exitItem})
        m_viewMenu.DropDownItems.Add(m_resetItem)
        m_helpMenu.DropDownItems.Add(m_aboutItem)
        m_menuStrip.Items.AddRange(New ToolStripItem() {m_fileMenu, m_viewMenu, m_helpMenu})
        m_menuStrip.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

End Class
