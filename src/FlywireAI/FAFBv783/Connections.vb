Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Connections (Filtered): ``connections_princeton.csv.gz`` (5 columns).
    ''' 
    ''' One row for every connected pair of cells and neuropil (region). Connections
    ''' with less than 5 synapses are filtered out.
    ''' </summary>
    ''' <remarks>
    ''' It is recommended to use the streaming reader for this table because the
    ''' uncompressed csv document contains millions of data rows.
    ''' </remarks>
    Public Class Connections

        ''' <summary>
        ''' FlyWire Root ID of the pre-synaptic (from) cell.
        ''' </summary>
        <Column("pre_root_id")>
        Public Property PreRootId As Long

        ''' <summary>
        ''' FlyWire Root ID of the post-synaptic (to) cell.
        ''' </summary>
        <Column("post_root_id")>
        Public Property PostRootId As Long

        ''' <summary>
        ''' Neuropil (brain region) abbreviation of the connection site.
        ''' </summary>
        <Column("neuropil")>
        Public Property Neuropil As String

        ''' <summary>
        ''' Number of synapses between the cell pair within the given neuropil.
        ''' </summary>
        <Column("syn_count")>
        Public Property SynCount As Double

        <Column("nt_type")>
        Public Property NtType As String

    End Class
End Namespace
