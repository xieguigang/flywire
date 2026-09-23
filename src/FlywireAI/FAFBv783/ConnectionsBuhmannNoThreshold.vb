Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Connections Predicted With Buhmann Et. Al. [Original Version Used Prior To July 2025]:
    ''' ``connections_buhmann_no_threshold.csv.gz`` (5 columns).
    ''' 
    ''' One row for every connected pair of cells and neuropil (region). The fifth
    ''' column contains the predicted neurotransmitter type for the synapses.
    ''' </summary>
    ''' <remarks>
    ''' This synapse table is no longer in use in Codex apps. It was replaced with an
    ''' updated version in July 2025, using an improved prediction method.
    ''' 
    ''' It is recommended to use the streaming reader for this table because the
    ''' uncompressed csv document contains more than 16 million data rows.
    ''' </remarks>
    Public Class ConnectionsBuhmannNoThreshold

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
