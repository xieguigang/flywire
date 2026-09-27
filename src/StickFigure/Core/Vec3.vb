Imports System.Math
Imports Microsoft.VisualBasic.Imaging.Drawing3D
Imports Microsoft.VisualBasic.Imaging.Physics
Imports std = System.Math

''' <summary>
''' 演示层使用的轻量三维向量。
''' </summary>
''' <remarks>
''' 之所以不直接使用 <see cref="Point3D"/>：它只重载了减号运算符，且
''' <c>Operator =(Point3D, Single)</c> 会把向量与标量比较，容易在业务代码里误用。
''' 也不直接使用 physics 的 <c>Vector3</c>：它是引用类型，逐点分配在渲染热路径上开销偏大。
''' 本类型只负责渲染 / 关卡几何的数学，物理量仍然用 <c>Vector3</c>。
''' </remarks>
Public Structure Vec3

    Public X As Double
    Public Y As Double
    Public Z As Double

    Sub New(x As Double, y As Double, z As Double)
        Me.X = x
        Me.Y = y
        Me.Z = z
    End Sub

    Public Shared ReadOnly Property Zero As Vec3
        Get
            Return New Vec3(0, 0, 0)
        End Get
    End Property

    Public Shared ReadOnly Property UnitX As Vec3
        Get
            Return New Vec3(1, 0, 0)
        End Get
    End Property

    Public Shared ReadOnly Property UnitY As Vec3
        Get
            Return New Vec3(0, 1, 0)
        End Get
    End Property

    Public Shared ReadOnly Property UnitZ As Vec3
        Get
            Return New Vec3(0, 0, 1)
        End Get
    End Property

    Public ReadOnly Property Length As Double
        Get
            Return std.Sqrt(X * X + Y * Y + Z * Z)
        End Get
    End Property

    Public ReadOnly Property LengthSquared As Double
        Get
            Return X * X + Y * Y + Z * Z
        End Get
    End Property

    Public Function Normalize() As Vec3
        Dim len As Double = Length
        If len < 1.0E-12 Then Return Vec3.Zero
        Return New Vec3(X / len, Y / len, Z / len)
    End Function

    Public Shared Function Dot(a As Vec3, b As Vec3) As Double
        Return a.X * b.X + a.Y * b.Y + a.Z * b.Z
    End Function

    Public Shared Function Cross(a As Vec3, b As Vec3) As Vec3
        Return New Vec3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X)
    End Function

    Public Shared Function Distance(a As Vec3, b As Vec3) As Double
        Return (a - b).Length
    End Function

    Public Shared Function Lerp(a As Vec3, b As Vec3, t As Double) As Vec3
        Return a + (b - a) * t
    End Function

    ''' <summary>去掉沿 <paramref name="unitAxis"/> 的分量。</summary>
    Public Function ProjectOnPlane(unitAxis As Vec3) As Vec3
        Return Me - unitAxis * Dot(Me, unitAxis)
    End Function

    ' /********************************************************************************/
    '  与外部类型的互转
    ' /********************************************************************************/

    ''' <summary>转换为渲染用的 <see cref="Point3D"/>。</summary>
    Public Function ToPoint3D() As Point3D
        Return New Point3D(X, Y, Z)
    End Function

    ''' <summary>由渲染用的 <see cref="Point3D"/> 构造。</summary>
    Public Shared Function FromPoint3D(p As Point3D) As Vec3
        Return New Vec3(p.X, p.Y, p.Z)
    End Function

    ''' <summary>转换为物理用的 <c>Vector3</c>。</summary>
    Public Function ToPhysics() As Vector3
        Return New Vector3(X, Y, Z)
    End Function

    ''' <summary>由物理用的 <c>Vector3</c> 构造。</summary>
    Public Shared Function FromPhysics(v As Vector3) As Vec3
        If v Is Nothing Then Return Vec3.Zero
        Return New Vec3(v.x, v.y, v.z)
    End Function

    ' /********************************************************************************/
    '  运算符
    ' /********************************************************************************/

    Public Shared Operator +(a As Vec3, b As Vec3) As Vec3
        Return New Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z)
    End Operator

    Public Shared Operator -(a As Vec3, b As Vec3) As Vec3
        Return New Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z)
    End Operator

    Public Shared Operator -(v As Vec3) As Vec3
        Return New Vec3(-v.X, -v.Y, -v.Z)
    End Operator

    Public Shared Operator *(v As Vec3, s As Double) As Vec3
        Return New Vec3(v.X * s, v.Y * s, v.Z * s)
    End Operator

    Public Shared Operator *(s As Double, v As Vec3) As Vec3
        Return New Vec3(v.X * s, v.Y * s, v.Z * s)
    End Operator

    Public Shared Operator /(v As Vec3, s As Double) As Vec3
        If std.Abs(s) < 1.0E-12 Then Return Vec3.Zero
        Return New Vec3(v.X / s, v.Y / s, v.Z / s)
    End Operator

    Public Overrides Function ToString() As String
        Return $"({X:F3}, {Y:F3}, {Z:F3})"
    End Function
End Structure
