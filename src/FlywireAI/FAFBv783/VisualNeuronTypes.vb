Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Visual Neuron Annotations: ``visual_neuron_types.csv.gz`` (95,079 rows, 6 columns).
    ''' 
    ''' Visual neuron types and type families in the right optic lobe.
    ''' </summary>
    Public Class VisualNeuronTypes

        <Column("root_id", GetType(Int64Parser))>
        Public Property RootId As Long

        ''' <summary>
        ''' Visual cell type, for example ``T5c``.
        ''' </summary>
        <Column("type")>
        Public Property [Type] As String

        ''' <summary>
        ''' Cell type family, for example ``T5 Neuron``.
        ''' </summary>
        <Column("family")>
        Public Property Family As String

        <Column("subsystem")>
        Public Property Subsystem As String

        <Column("category")>
        Public Property Category As String

        <Column("side")>
        Public Property Side As String

    End Class
End Namespace
