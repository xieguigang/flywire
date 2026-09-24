Imports RibbonLib

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class FormMain
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
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

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        VS2015LightTheme1 = New ThemeVS2015.VS2015LightTheme()
        DockPanel1 = New Microsoft.VisualStudio.WinForms.Docking.DockPanel()
        VisualStudioToolStripExtender1 = New Microsoft.VisualStudio.WinForms.Docking.VisualStudioToolStripExtender(components)
        Ribbon1 = New Ribbon()
        StatusStrip1 = New StatusStrip()
        m_statusText = New ToolStripStatusLabel()
        ToolStripStatusLabel1 = New ToolStripStatusLabel()
        m_progress = New ToolStripProgressBar()
        m_sceneText = New ToolStripStatusLabel()
        StatusStrip1.SuspendLayout()
        SuspendLayout()
        ' 
        ' DockPanel1
        ' 
        DockPanel1.Dock = DockStyle.Fill
        DockPanel1.Location = New Point(0, 116)
        DockPanel1.Name = "DockPanel1"
        DockPanel1.Size = New Size(1428, 705)
        DockPanel1.TabIndex = 0
        ' 
        ' VisualStudioToolStripExtender1
        ' 
        VisualStudioToolStripExtender1.DefaultRenderer = Nothing
        ' 
        ' Ribbon1
        ' 
        Ribbon1.Location = New Point(0, 0)
        Ribbon1.Name = "Ribbon1"
        Ribbon1.ResourceIdentifier = Nothing
        Ribbon1.ResourceName = "Neuropils.RibbonMarkup.ribbon"
        Ribbon1.ShortcutTableResourceName = Nothing
        Ribbon1.Size = New Size(1428, 116)
        Ribbon1.TabIndex = 1
        ' 
        ' StatusStrip1
        ' 
        StatusStrip1.Items.AddRange(New ToolStripItem() {m_statusText, ToolStripStatusLabel1, m_progress, m_sceneText})
        StatusStrip1.Location = New Point(0, 821)
        StatusStrip1.Name = "StatusStrip1"
        StatusStrip1.Size = New Size(1428, 22)
        StatusStrip1.TabIndex = 2
        StatusStrip1.Text = "StatusStrip1"
        ' 
        ' m_statusText
        ' 
        m_statusText.Name = "m_statusText"
        m_statusText.Size = New Size(42, 17)
        m_statusText.Text = "Ready!"
        ' 
        ' ToolStripStatusLabel1
        ' 
        ToolStripStatusLabel1.Name = "ToolStripStatusLabel1"
        ToolStripStatusLabel1.Size = New Size(1223, 17)
        ToolStripStatusLabel1.Spring = True
        ' 
        ' m_progress
        ' 
        m_progress.Name = "m_progress"
        m_progress.Size = New Size(100, 16)
        m_progress.Style = ProgressBarStyle.Marquee
        ' 
        ' m_sceneText
        ' 
        m_sceneText.Name = "m_sceneText"
        m_sceneText.Size = New Size(46, 17)
        m_sceneText.Text = "Loaded"
        ' 
        ' FormMain
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1428, 843)
        Controls.Add(DockPanel1)
        Controls.Add(StatusStrip1)
        Controls.Add(Ribbon1)
        Name = "FormMain"
        Text = "Neuropils"
        StatusStrip1.ResumeLayout(False)
        StatusStrip1.PerformLayout()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents VS2015LightTheme1 As ThemeVS2015.VS2015LightTheme
    Friend WithEvents DockPanel1 As Microsoft.VisualStudio.WinForms.Docking.DockPanel
    Friend WithEvents VisualStudioToolStripExtender1 As Microsoft.VisualStudio.WinForms.Docking.VisualStudioToolStripExtender
    Friend WithEvents Ribbon1 As Global.RibbonLib.Ribbon
    Friend WithEvents StatusStrip1 As StatusStrip
    Friend WithEvents m_statusText As ToolStripStatusLabel
    Friend WithEvents m_progress As ToolStripProgressBar
    Friend WithEvents m_sceneText As ToolStripStatusLabel
    Friend WithEvents ToolStripStatusLabel1 As ToolStripStatusLabel
End Class
