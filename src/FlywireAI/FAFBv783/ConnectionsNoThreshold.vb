Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Connections (Unfiltered): ``connections_princeton_no_threshold.csv.gz`` (5 columns).
    ''' 
    ''' Same schema as <see cref="Connections"/>, but without the 5 synapses
    ''' threshold filter, hence the row number is much larger.
    ''' </summary>
    ''' <remarks>
    ''' It is recommended to use the streaming reader for this table because the
    ''' uncompressed csv document contains tens of millions of data rows.
    ''' </remarks>
    Public Class ConnectionsNoThreshold

        <Column("pre_root_id")>
        Public Property PreRootId As Long

        <Column("post_root_id")>
        Public Property PostRootId As Long

        <Column("neuropil")>
        Public Property Neuropil As String

        <Column("syn_count")>
        Public Property SynCount As Double

        <Column("nt_type")>
        Public Property NtType As String

    End Class
End Namespace
