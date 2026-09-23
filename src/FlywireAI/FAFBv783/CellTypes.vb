Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Cell Types: ``consolidated_cell_types.csv.gz`` (138,327 rows, 3 columns).
    ''' 
    ''' Consolidated from multiple annotation sources. Primary cell type picked by
    ''' prioritizing most recent and most specific annotations.
    ''' </summary>
    Public Class CellTypes

        ''' <summary>
        ''' FlyWire Root ID of the cell.
        ''' </summary>
        <Column("root_id", GetType(Int64Parser))>
        Public Property RootId As Long

        <Column("primary_type")>
        Public Property PrimaryType As String

        <Column("additional_type(s)")>
        Public Property AdditionalTypes As String

    End Class
End Namespace
