Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Connectivity Tags: ``connectivity_tags.csv.gz`` (134,437 rows, 2 columns).
    ''' 
    ''' Descriptors for neuron connectivity characteristics, derived from network
    ''' analysis. Each neuron can have zero or more tags, separated by commas in the
    ''' second column.
    ''' </summary>
    Public Class ConnectivityTags

        <Column("root_id")>
        Public Property RootId As Long

        ''' <summary>
        ''' Comma separated connectivity tags, for example
        ''' ``reciprocal,feedforward_loop_participant``.
        ''' </summary>
        <Column("connectivity_tag")>
        Public Property ConnectivityTag As String

    End Class
End Namespace
