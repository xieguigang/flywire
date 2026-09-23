Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Community Labels (Raw): ``labels.csv.gz`` (160,045 rows, 9 columns).
    ''' 
    ''' Community identification labels / tags, updated daily. Includes information
    ''' about contributors and their affiliation. Each cell (root_id) might have zero
    ''' or more labels assigned to it - hence the ``root_id`` column is not unique.
    ''' Additionally, this table contains raw labels, as they were submitted by the
    ''' FlyWire community.
    ''' </summary>
    Public Class CommunityLabels

        <Column("root_id")>
        Public Property RootId As Long

        <Column("label")>
        Public Property Label As String

        <Column("user_id")>
        Public Property UserId As Long

        ''' <summary>
        ''' Marked position, formatted as ``[x y z]``.
        ''' </summary>
        <Column("position")>
        Public Property Position As String

        <Column("supervoxel_id")>
        Public Property SupervoxelId As Long

        <Column("label_id")>
        Public Property LabelId As Long

        ''' <summary>
        ''' Creation time of the label, for example ``2022-02-07 04:55:09``.
        ''' </summary>
        <Column("date_created")>
        Public Property DateCreated As String

        <Column("user_name")>
        Public Property UserName As String

        <Column("user_affiliation")>
        Public Property UserAffiliation As String

    End Class
End Namespace
