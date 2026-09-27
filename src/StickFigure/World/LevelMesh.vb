Imports System.Drawing
Imports System.Runtime.CompilerServices
Imports Microsoft.VisualBasic.Imaging.Drawing3D

''' <summary>关卡中的一块三角面（渲染用，物理由 <see cref="LevelBuilder"/> 单独注册刚体）。</summary>
Public Structure LevelTriangle

    Public A As Vec3
    Public B As Vec3
    Public C As Vec3
    ''' <summary>面颜色。</summary>
    Public Color As Color

    Sub New(a As Vec3, b As Vec3, c As Vec3, color As Color)
        Me.A = a
        Me.B = b
        Me.C = c
        Me.Color = color
    End Sub

    ''' <summary>面中心（画家算法排序用）。</summary>
    Public ReadOnly Property Center As Vec3
        Get
            Return (A + B + C) / 3.0
        End Get
    End Property

    ''' <summary>面法向（未归一化，长度正比于面积）。</summary>
    Public ReadOnly Property Normal As Vec3
        Get
            Return Vec3.Cross(B - A, C - A)
        End Get
    End Property
End Structure

''' <summary>关卡中的一条线段（地面网格、辅助线）。</summary>
Public Structure LevelLine

    Public A As Vec3
    Public B As Vec3
    Public Color As Color

    Sub New(a As Vec3, b As Vec3, color As Color)
        Me.A = a
        Me.B = b
        Me.Color = color
    End Sub
End Structure

''' <summary>
''' 静态关卡的渲染几何容器。主视角与头部视角共用同一份数据。
''' </summary>
''' <remarks>
''' <see cref="Solids"/> 里的三角面会被提交给 <c>DxScene3DCanvas</c> 作为静态 <c>Surface</c>
''' （只加载一次，避免 <c>Scene</c> 每次装载都按质心重新居中导致的整体漂移）；
''' <see cref="Ground"/> 只用于头部视角，因为主视角自带地面绘制。
''' </remarks>
Public Class LevelMesh

    ''' <summary>实体几何：障碍物、台阶、平台。进入主视角的静态面集合。</summary>
    Public ReadOnly Property Solids As New List(Of LevelTriangle)()

    ''' <summary>地面面片：只进入头部视角（主视角用控件自带的地面）。</summary>
    Public ReadOnly Property Ground As New List(Of LevelTriangle)()

    ''' <summary>网格与辅助线：两个视角都会绘制。</summary>
    Public ReadOnly Property GridLines As New List(Of LevelLine)()

    ''' <summary>加入一个以 <paramref name="center"/> 为中心、尺寸 <paramref name="size"/> 的长方体（6 面 12 三角）。</summary>
    Public Sub AddBox(center As Vec3, size As Vec3, color As Color, Optional topColor As Color = Nothing)
        Dim hx As Double = size.X * 0.5
        Dim hy As Double = size.Y * 0.5
        Dim hz As Double = size.Z * 0.5
        Dim top As Color = If(topColor.IsEmpty, Lighten(color, 0.18), topColor)

        Dim p000 As New Vec3(center.X - hx, center.Y - hy, center.Z - hz)
        Dim p100 As New Vec3(center.X + hx, center.Y - hy, center.Z - hz)
        Dim p110 As New Vec3(center.X + hx, center.Y - hy, center.Z + hz)
        Dim p010 As New Vec3(center.X - hx, center.Y - hy, center.Z + hz)
        Dim p001 As New Vec3(center.X - hx, center.Y + hy, center.Z - hz)
        Dim p101 As New Vec3(center.X + hx, center.Y + hy, center.Z - hz)
        Dim p111 As New Vec3(center.X + hx, center.Y + hy, center.Z + hz)
        Dim p011 As New Vec3(center.X - hx, center.Y + hy, center.Z + hz)

        ' top / bottom
        Call AddQuad(p001, p101, p111, p011, top)
        Call AddQuad(p010, p110, p100, p000, Darken(color, 0.45))
        ' sides
        Call AddQuad(p000, p100, p101, p001, Darken(color, 0.85))
        Call AddQuad(p110, p010, p011, p111, color)
        Call AddQuad(p100, p110, p111, p101, Darken(color, 0.7))
        Call AddQuad(p010, p000, p001, p011, Darken(color, 0.6))
    End Sub

    ''' <summary>加入一个四边形（拆成两个三角面）。</summary>
    Public Sub AddQuad(a As Vec3, b As Vec3, c As Vec3, d As Vec3, color As Color)
        Solids.Add(New LevelTriangle(a, b, c, color))
        Solids.Add(New LevelTriangle(a, c, d, color))
    End Sub

    ''' <summary>加入一个地面四边形（只进入头部视角）。</summary>
    Public Sub AddGroundQuad(a As Vec3, b As Vec3, c As Vec3, d As Vec3, color As Color)
        Ground.Add(New LevelTriangle(a, b, c, color))
        Ground.Add(New LevelTriangle(a, c, d, color))
    End Sub

    ''' <summary>在 y=<paramref name="y"/> 平面上生成网格线。</summary>
    Public Sub AddGrid(minX As Double, maxX As Double, minZ As Double, maxZ As Double,
                       stepSize As Double, y As Double, color As Color)
        Dim x As Double = minX

        While x <= maxX + 1.0E-9
            GridLines.Add(New LevelLine(New Vec3(x, y, minZ), New Vec3(x, y, maxZ), color))
            x += stepSize
        End While

        Dim z As Double = minZ

        While z <= maxZ + 1.0E-9
            GridLines.Add(New LevelLine(New Vec3(minX, y, z), New Vec3(maxX, y, z), color))
            z += stepSize
        End While
    End Sub

    ''' <summary>把 <see cref="Solids"/> 转成可直接提交给 <c>DxScene3DCanvas</c> 的 <see cref="Surface"/> 数组。</summary>
    Public Function ToSurfaces() As Surface()
        Dim result(Solids.Count - 1) As Surface

        For i As Integer = 0 To Solids.Count - 1
            Dim t As LevelTriangle = Solids(i)

            result(i) = New Surface With {
                .vertices = {t.A.ToPoint3D(), t.B.ToPoint3D(), t.C.ToPoint3D()},
                .brush = New SolidBrush(t.Color)
            }
        Next

        Return result
    End Function

    ''' <summary>提亮。</summary>
    Public Shared Function Lighten(c As Color, amount As Double) As Color
        Return Mix(c, Color.White, amount)
    End Function

    ''' <summary>压暗。</summary>
    Public Shared Function Darken(c As Color, amount As Double) As Color
        Return Mix(c, Color.Black, 1.0 - amount)
    End Function

    Private Shared Function Mix(a As Color, b As Color, t As Double) As Color
        t = Math.Max(0.0, Math.Min(1.0, t))

        Return Color.FromArgb(
            CInt(a.R + (b.R - a.R) * t),
            CInt(a.G + (b.G - a.G) * t),
            CInt(a.B + (b.B - a.B) * t))
    End Function
End Class
