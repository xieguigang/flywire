Imports System.Drawing
Imports System.Math
Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D
Imports Microsoft.VisualBasic.Imaging.Drawing3D
Imports Microsoft.VisualBasic.Imaging.Physics
Imports std = System.Math

''' <summary>
''' 把火柴人骨架转成渲染图层：骨骼线段 + 头部线框球 + 关节十字标记。
''' </summary>
''' <remarks>
''' 骨骼全部用线段表达而不是实体网格，这样每帧只要调用
''' <c>DxScene3DCanvas.UpdateConnections</c>：连线不参与 <c>Scene</c> 的质心计算，
''' 静态环境面只需加载一次，人物走到哪里都不会让整个场景发生漂移。
''' </remarks>
Public Module FigureVisual

    ''' <summary>头部线框颜色。</summary>
    Public ReadOnly Property HeadColor As Color = Color.FromArgb(&HE8, &HEE, &HF6)
    ''' <summary>躯干颜色。</summary>
    Public ReadOnly Property TorsoColor As Color = Color.FromArgb(&H7C, &HE3, &H8B)
    ''' <summary>左侧肢体颜色。</summary>
    Public ReadOnly Property LeftColor As Color = Color.FromArgb(&H4E, &HA8, &HFF)
    ''' <summary>右侧肢体颜色。</summary>
    Public ReadOnly Property RightColor As Color = Color.FromArgb(&HFF, &HD1, &H66)
    ''' <summary>关节标记颜色。</summary>
    Public ReadOnly Property JointColor As Color = Color.FromArgb(&H8A, &H9B, &HAE)

    ''' <summary>各骨骼使用的颜色。</summary>
    Private Function BoneColor(index As BoneIndex) As Color
        Select Case index
            Case BoneIndex.Head
                Return HeadColor
            Case BoneIndex.Pelvis, BoneIndex.Chest, BoneIndex.Neck
                Return TorsoColor
            Case BoneIndex.UpperArmL, BoneIndex.LowerArmL, BoneIndex.ThighL, BoneIndex.ShinL, BoneIndex.FootL
                Return LeftColor
            Case Else
                Return RightColor
        End Select
    End Function

    ''' <summary>
    ''' 生成火柴人的全部渲染线段。
    ''' </summary>
    ''' <param name="includeJoints">是否绘制关节十字标记。</param>
    ''' <param name="includeHead">是否绘制头部线框球。</param>
    Public Function BuildFigureLines(skel As StickmanSkeleton,
                                     Optional includeJoints As Boolean = True,
                                     Optional includeHead As Boolean = True) As List(Of LevelLine)
        Dim lines As New List(Of LevelLine)()

        ' ---------- 骨骼 ----------
        For Each index As BoneIndex In System.Enum.GetValues(GetType(BoneIndex)).Cast(Of BoneIndex)()
            Dim seg As (A As Vec3, B As Vec3) = skel.BoneSegment(index)

            lines.Add(New LevelLine(seg.A, seg.B, BoneColor(index)))
        Next

        ' ---------- 头部线框球：三个正交圆环 ----------
        If includeHead Then
            Call lines.AddRange(HeadWireframe(skel))
        End If

        ' ---------- 关节十字 ----------
        If includeJoints Then
            Call lines.AddRange(JointMarkers(skel))
        End If

        Return lines
    End Function

    ''' <summary>以头部为中心的三个正交圆环（线框球）。</summary>
    Public Function HeadWireframe(skel As StickmanSkeleton, Optional segments As Integer = 12,
                                   Optional scale As Double = 1.25) As List(Of LevelLine)
        Dim result As New List(Of LevelLine)()
        Dim head As RigidBody3D = skel.Bodies(BoneIndex.Head)
        Dim center As Vec3 = Vec3.FromPhysics(head.Position)
        Dim radius As Double = 0.115 * scale
        Dim right As Vec3 = Vec3.FromPhysics(head.Right).Normalize()
        Dim up As Vec3 = Vec3.FromPhysics(head.Up).Normalize()
        Dim fwd As Vec3 = Vec3.FromPhysics(head.Forward).Normalize()

        Call AddCircle(result, center, right, up, radius, segments, HeadColor)
        Call AddCircle(result, center, up, fwd, radius, segments, HeadColor)
        Call AddCircle(result, center, fwd, right, radius, segments, HeadColor)

        ' 视线方向的一小段指示线，便于观察头部朝向
        result.Add(New LevelLine(center + fwd * radius, center + fwd * (radius + 0.16), RightColor))

        Return result
    End Function

    Private Sub AddCircle(target As List(Of LevelLine), center As Vec3,
                          axisU As Vec3, axisV As Vec3, radius As Double,
                          segments As Integer, color As Color)
        Dim previous As Vec3 = center + axisU * radius

        For i As Integer = 1 To segments
            Dim t As Double = 2.0 * std.PI * i / segments
            Dim p As Vec3 = center + axisU * (std.Cos(t) * radius) + axisV * (std.Sin(t) * radius)

            target.Add(New LevelLine(previous, p, color))
            previous = p
        Next
    End Sub

    ''' <summary>在每个关节锚点处画一个小十字。</summary>
    Public Function JointMarkers(skel As StickmanSkeleton, Optional size As Double = 0.045) As List(Of LevelLine)
        Dim result As New List(Of LevelLine)()

        For Each index As JointIndex In System.Enum.GetValues(GetType(JointIndex)).Cast(Of JointIndex)()
            Dim p As Vec3 = skel.JointPosition(index)

            result.Add(New LevelLine(p - New Vec3(size, 0, 0), p + New Vec3(size, 0, 0), JointColor))
            result.Add(New LevelLine(p - New Vec3(0, size, 0), p + New Vec3(0, size, 0), JointColor))
            result.Add(New LevelLine(p - New Vec3(0, 0, size), p + New Vec3(0, 0, size), JointColor))
        Next

        Return result
    End Function

    ''' <summary>
    ''' 世界坐标（Y 轴向上，物理引擎的约定）→ 场景坐标（Z 轴向上，<c>DxScene3DCanvas</c> 的约定）。
    ''' </summary>
    ''' <remarks>
    ''' 映射 <c>(x,y,z) → (x,−z,y)</c> 是一次绕 X 轴的 −90° 旋转，保持右手系，
    ''' 因此控件自带的地面平面（按场景最低 Z 绘制）才会落在我们世界的 y=0 上。
    ''' </remarks>
    Public Function ToScene(p As Vec3) As Point3D
        Return New Point3D(p.X, -p.Z, p.Y)
    End Function

    ''' <summary>场景坐标 → 世界坐标。</summary>
    Public Function FromScene(p As Point3D) As Vec3
        Return New Vec3(p.X, p.Z, -p.Y)
    End Function

    ''' <summary>把关卡线段转成可以提交给 <c>DxScene3DCanvas</c> 的 <see cref="LineSegment"/>。</summary>
    Public Function ToLineSegments(lines As IEnumerable(Of LevelLine)) As LineSegment()
        Return lines.Select(Function(l) New LineSegment(ToScene(l.A), ToScene(l.B), l.Color)).ToArray()
    End Function
End Module
