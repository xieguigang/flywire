Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Marked Neuron Coordinates: ``coordinates.csv.gz`` (3 columns).
    ''' 
    ''' FlyWire Supervoxel IDs and position coordinates (in nanometers) for cells in
    ''' the dataset. One cell might have zero or more coordinates and supervoxel IDs,
    ''' depending on marked positions during human proofreading / cell identification.
    ''' </summary>
    Public Class Coordinates

        <Column("root_id", GetType(Int64Parser))>
        Public Property RootId As Long

        ''' <summary>
        ''' Marked position (in nanometer), formatted as ``[x y z]``.
        ''' </summary>
        <Column("position")>
        Public Property Position As String

        <Column("supervoxel_id", GetType(Int64Parser))>
        Public Property SupervoxelId As Long

    End Class
End Namespace
