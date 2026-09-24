' AboutForm 的界面布局（Windows 窗体设计器维护的声明式代码）。
' 这里只放"控件怎么摆"的代码：只读多行 TextBox 装说明文本、底部一个确定按钮；
' 说明文本本身由 AboutForm.vb 的 Load 事件灌入 m_textBox（从原 PageFlywireCanvas 的 onAbout 搬过来）。
Imports Galaxy.Workbench.CommonDialogs

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class AboutForm
    Inherits InputDialog

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
        components = New ComponentModel.Container()
        m_textBox = New TextBox()
        m_okButton = New Button()
        VisualStudioToolStripExtender1 = New Microsoft.VisualStudio.WinForms.Docking.VisualStudioToolStripExtender(components)
        SuspendLayout()
        ' 
        ' m_textBox
        ' 
        m_textBox.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        m_textBox.Location = New Point(12, 12)
        m_textBox.Multiline = True
        m_textBox.Name = "m_textBox"
        m_textBox.ReadOnly = True
        m_textBox.ScrollBars = ScrollBars.Vertical
        m_textBox.Size = New Size(560, 360)
        m_textBox.TabIndex = 0
        m_textBox.TabStop = False
        m_textBox.WordWrap = False
        ' 
        ' m_okButton
        ' 
        m_okButton.Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        m_okButton.DialogResult = DialogResult.OK
        m_okButton.Location = New Point(497, 384)
        m_okButton.Name = "m_okButton"
        m_okButton.Size = New Size(75, 23)
        m_okButton.TabIndex = 1
        m_okButton.Text = "确定"
        m_okButton.UseVisualStyleBackColor = True
        ' 
        ' VisualStudioToolStripExtender1
        ' 
        VisualStudioToolStripExtender1.DefaultRenderer = Nothing
        ' 
        ' AboutForm
        ' 
        AcceptButton = m_okButton
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(584, 419)
        Controls.Add(m_textBox)
        Controls.Add(m_okButton)
        FormBorderStyle = FormBorderStyle.FixedDialog
        Name = "AboutForm"
        Padding = New Padding(12)
        ShowIcon = False
        ShowInTaskbar = False
        Text = "关于 Neuropils"
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Private WithEvents m_textBox As TextBox
    Private WithEvents m_okButton As Button
    Friend WithEvents VisualStudioToolStripExtender1 As Microsoft.VisualStudio.WinForms.Docking.VisualStudioToolStripExtender
End Class
