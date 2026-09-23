Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Classification / Hierarchical Annotations: ``classification.csv.gz``
    ''' (139,255 rows, 8 columns).
    ''' 
    ''' Hierarchical Classification, Soma Side, Hemilineage and Nerve Type for each
    ''' cell in the dataset.
    ''' </summary>
    ''' <remarks>
    ''' The origin table description: top hierarchical annotation describing signal
    ''' flow of this neuron is the ``flow`` column; ``super_class`` is the broadest
    ''' hierarchical annotation below flow; ``class`` and ``sub_class`` sit between
    ''' superclass and cell type; ``side`` is the soma side of the neuron in fly's
    ''' perspective (corrected for the left/right inversion of the FAFB image data);
    ''' ``nerve`` is the nerve through which the neuron is entering/exiting the
    ''' nervous system; ``hemilineage`` is the developmental (hemi-)lineage.
    ''' </remarks>
    Public Class Classification

        <Column("root_id", GetType(Int64Parser))>
        Public Property RootId As Long

        <Column("flow")>
        Public Property Flow As String

        <Column("super_class")>
        Public Property SuperClass As String

        ''' <summary>
        ''' ``class`` is a VisualBasic language keyword, so the property name is escaped.
        ''' </summary>
        <Column("class")>
        Public Property [Class] As String

        <Column("sub_class")>
        Public Property SubClass As String

        <Column("hemilineage")>
        Public Property Hemilineage As String

        <Column("side")>
        Public Property Side As String

        <Column("nerve")>
        Public Property Nerve As String

    End Class
End Namespace
