Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Cell Size Measurements: ``cell_stats.csv.gz`` (139,246 rows, 4 columns).
    ''' 
    ''' Specifies the surface area, cable length and size/volume for each cell in the
    ''' dataset. Nanometer units.
    ''' </summary>
    Public Class CellStats

        <Column("root_id", GetType(Int64Parser))>
        Public Property RootId As Long

        ''' <summary>
        ''' Total cable length of the cell, in nanometer.
        ''' </summary>
        <Column("length_nm")>
        Public Property LengthNm As Double

        ''' <summary>
        ''' Surface area of the cell, in nanometer.
        ''' </summary>
        <Column("area_nm")>
        Public Property AreaNm As Double

        ''' <summary>
        ''' Size / volume of the cell, in nanometer.
        ''' </summary>
        <Column("size_nm")>
        Public Property SizeNm As Double

    End Class
End Namespace
