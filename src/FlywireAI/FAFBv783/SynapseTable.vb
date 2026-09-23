Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Synapse Table: ``fafb_v783_princeton_synapse_table.csv.gz`` (80,215,790 rows, 13 columns).
    ''' 
    ''' Individual synapse sizes and coordinates (x, y, z for pre/center/post sites in
    ''' nanometers) for synaptic connections between proofread neurons (including
    ''' autapse predictions). For compactness, the common prefix in the pre/post cell
    ''' Root IDs is omitted - it is specified in the column headers.
    ''' </summary>
    ''' <remarks>
    ''' It is strongly recommended to use the streaming reader for this table: the
    ''' uncompressed csv document contains more than 80 million data rows.
    ''' 
    ''' The ``pre_root_id_720575940`` and ``post_root_id_720575940`` columns only contain
    ''' the last X digits of the cell Root ID; for the full Root ID the ``720575940``
    ''' prefix that is specified in the column header should be added.
    ''' </remarks>
    Public Class SynapseTable

        <Column("pre_x")>
        Public Property PreX As Double

        <Column("pre_y")>
        Public Property PreY As Double

        <Column("pre_z")>
        Public Property PreZ As Double

        <Column("ctr_x")>
        Public Property CtrX As Double

        <Column("ctr_y")>
        Public Property CtrY As Double

        <Column("ctr_z")>
        Public Property CtrZ As Double

        <Column("post_x")>
        Public Property PostX As Double

        <Column("post_y")>
        Public Property PostY As Double

        <Column("post_z")>
        Public Property PostZ As Double

        ''' <summary>
        ''' Number of voxels detected, the size of the synapse.
        ''' </summary>
        <Column("size")>
        Public Property Size As Double

        ''' <summary>
        ''' Last digits of the pre-synaptic cell Root ID.
        ''' </summary>
        <Column("pre_root_id_720575940", GetType(Int64Parser))>
        Public Property PreRootId720575940 As Long

        ''' <summary>
        ''' Last digits of the post-synaptic cell Root ID.
        ''' </summary>
        <Column("post_root_id_720575940", GetType(Int64Parser))>
        Public Property PostRootId720575940 As Long

        ''' <summary>
        ''' Brain region containing the synapse; may be empty.
        ''' </summary>
        <Column("neuropil")>
        Public Property Neuropil As String

    End Class
End Namespace
