Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Community Labels (Refined): ``processed_labels.csv.gz`` (2 columns).
    ''' 
    ''' Processed community identification labels: cleaned, deduplicated, removed
    ''' un-informative or incorrect parts. This is the set of labels used in the
    ''' Codex search app.
    ''' </summary>
    Public Class ProcessedLabels

        <Column("root_id")>
        Public Property RootId As Long

        ''' <summary>
        ''' Refined labels of the cell, formatted as ``['T4b; FBbt_00003733']``.
        ''' </summary>
        <Column("processed_labels")>
        Public Property ProcessedLabelsList As String

    End Class
End Namespace
