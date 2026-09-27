<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class FormStickFigure
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

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        FigureCanvas1 = New FigureCanvas()
        SuspendLayout()
        ' 
        ' FigureCanvas1
        ' 
        FigureCanvas1.Dock = DockStyle.Fill
        FigureCanvas1.Location = New Point(0, 0)
        FigureCanvas1.Name = "FigureCanvas1"
        FigureCanvas1.Size = New Size(943, 570)
        FigureCanvas1.TabIndex = 0
        ' 
        ' FormStickFigure
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(943, 570)
        Controls.Add(FigureCanvas1)
        Name = "FormStickFigure"
        Text = "Form1"
        ResumeLayout(False)
    End Sub

    Friend WithEvents FigureCanvas1 As FigureCanvas

End Class
