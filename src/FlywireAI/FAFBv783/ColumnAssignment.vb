Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Visual Neuron Columns: ``column_assignment.csv.gz`` (45,528 rows, 8 columns).
    ''' 
    ''' Visual column and retinotopic coordinate assignments for columnar cell types.
    ''' Available for both optic lobes (left and right).
    ''' </summary>
    Public Class ColumnAssignment

        <Column("root_id")>
        Public Property RootId As Long

        <Column("hemisphere")>
        Public Property Hemisphere As String

        <Column("type")>
        Public Property [Type] As String

        ''' <summary>
        ''' Visual column index of the cell, for example ``97``.
        ''' </summary>
        <Column("column_id")>
        Public Property ColumnId As Double

        <Column("x")>
        Public Property X As Double

        <Column("y")>
        Public Property Y As Double

        <Column("p")>
        Public Property P As Double

        <Column("q")>
        Public Property Q As Double

    End Class
End Namespace
