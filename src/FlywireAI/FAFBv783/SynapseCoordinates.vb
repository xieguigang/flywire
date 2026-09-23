Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Synapse Coordinates [Original Version Used Prior To July 2025]:
    ''' ``synapse_coordinates.csv.gz`` (5 columns).
    ''' 
    ''' Individual synapse coordinates (x, y, z in nanometers). The pre/post cell Root
    ''' ID columns can be empty for the non-proofread synapses, hence they are read as
    ''' string values.
    ''' </summary>
    ''' <remarks>
    ''' It is recommended to use the streaming reader for this table because the
    ''' uncompressed csv document contains tens of millions of data rows.
    ''' </remarks>
    Public Class SynapseCoordinates

        ''' <summary>
        ''' FlyWire Root ID of the pre-synaptic cell; empty for non-proofread synapses.
        ''' </summary>
        <Column("pre_root_id")>
        Public Property PreRootId As String

        ''' <summary>
        ''' FlyWire Root ID of the post-synaptic cell; empty for non-proofread synapses.
        ''' </summary>
        <Column("post_root_id")>
        Public Property PostRootId As String

        <Column("x")>
        Public Property X As Double

        <Column("y")>
        Public Property Y As Double

        <Column("z")>
        Public Property Z As Double

    End Class
End Namespace
