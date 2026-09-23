Imports System.Runtime.CompilerServices

Namespace FAFBv783

    ''' <summary>
    ''' SWC 骨架文件之中坐标与半径的单位。
    ''' </summary>
    ''' <remarks>
    ''' 注意：``docs/fafb-v783-data.md`` 之中描述 Neuron Skeletons 的单位为 micron，但是
    ''' ``sk_lod1_783_healed.zip`` 之中的实际文件通过 ``# Meta`` 声明为 ``1 nanometer``，
    ''' 并且坐标的数值量级 (x 大约为 1.9e5) 也证实了实际单位是纳米。
    ''' 因此 <see cref="SwcNode"/> 按照文件原值保存数据，同时记录单位并提供换算访问器。
    ''' </remarks>
    Public Enum SwcUnits

        ''' <summary>
        ''' 无法从文件的头部信息之中识别的单位。
        ''' </summary>
        Unknown = 0

        ''' <summary>
        ''' 纳米 (nm)。
        ''' </summary>
        Nanometer = 1

        ''' <summary>
        ''' 微米 (µm)。
        ''' </summary>
        Micrometer = 2

    End Enum

    ''' <summary>
    ''' SWC 单位相关的帮助函数。
    ''' </summary>
    Public Module SwcUnitExtensions

        ''' <summary>
        ''' 从 ``# Meta`` 之中的 ``units`` 文本解析出单位。
        ''' 
        ''' 例如 ``1 nanometer`` -> <see cref="SwcUnits.Nanometer"/>。
        ''' </summary>
        ''' <param name="text"></param>
        ''' <returns></returns>
        <Extension>
        Public Function ParseSwcUnits(text As String) As SwcUnits
            If String.IsNullOrWhiteSpace(text) Then
                Return SwcUnits.Unknown
            End If

            Dim tokens As String() = text.ToLower _
                .Split({" "c, "="c, ":"c, "("c, ")"c, ","c, "["c, "]"c}, StringSplitOptions.RemoveEmptyEntries)

            For Each token As String In tokens
                Select Case token
                    Case "nm", "nanometer", "nanometers", "nanometre", "nanometres"
                        Return SwcUnits.Nanometer
                    Case "um", "µm", "micron", "microns", "micrometer", "micrometers", "micrometre"
                        Return SwcUnits.Micrometer
                End Select
            Next

            Return SwcUnits.Unknown
        End Function

        ''' <summary>
        ''' 将指定单位下的长度值换算为纳米。
        ''' </summary>
        <Extension>
        Public Function ToNanometer(value As Double, unit As SwcUnits) As Double
            If unit = SwcUnits.Micrometer Then
                Return value * 1000
            Else
                Return value
            End If
        End Function

        ''' <summary>
        ''' 将指定单位下的长度值换算为微米。
        ''' </summary>
        <Extension>
        Public Function ToMicrometer(value As Double, unit As SwcUnits) As Double
            If unit = SwcUnits.Micrometer Then
                Return value
            Else
                Return value / 1000
            End If
        End Function

        ''' <summary>
        ''' 单位名称。
        ''' </summary>
        <Extension>
        Public Function ToUnitName(unit As SwcUnits) As String
            Select Case unit
                Case SwcUnits.Nanometer
                    Return "nanometer"
                Case SwcUnits.Micrometer
                    Return "micrometer"
                Case Else
                    Return "unknown"
            End Select
        End Function

    End Module

    ''' <summary>
    ''' SWC 骨架文件头部 ``# Meta: {...}`` 行所声明的骨架元数据。
    ''' 
    ''' 例如: ``# Meta: {"id": "720575940590515268", "name": "None", "units": "1 nanometer"}``
    ''' </summary>
    Public Class SwcMeta

        ''' <summary>
        ''' 骨架所对应的 FlyWire Root ID 文本。
        ''' </summary>
        Public Property id As String

        ''' <summary>
        ''' 骨架名称，通常是 ``None``。
        ''' </summary>
        Public Property name As String

        ''' <summary>
        ''' 单位声明文本，例如 ``1 nanometer``。
        ''' </summary>
        Public Property units As String

        ''' <summary>
        ''' ``units`` 字段所解析出来的单位。
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Unit As SwcUnits
            Get
                Return ParseSwcUnits(units)
            End Get
        End Property

        ''' <summary>
        ''' 元数据对象的摘要文本。
        ''' </summary>
        Public Overrides Function ToString() As String
            Return $"id={id}, name={name}, units={units}"
        End Function

    End Class

    ''' <summary>
    ''' SWC 文件头部 ``# Labels:`` 段所声明的节点类型定义。
    ''' </summary>
    Public Class SwcLabel

        ''' <summary>
        ''' 节点的类型编号。
        ''' </summary>
        Public Property Value As Integer

        ''' <summary>
        ''' 类型编号的名称，例如 ``soma`` / ``fork point`` / ``end point``。
        ''' </summary>
        Public Property Name As String

        Public Overrides Function ToString() As String
            Return $"{Value} = {Name}"
        End Function

    End Class

    ''' <summary>
    ''' 骨架之中一个节点的数据记录：``# n label x y z radius parent``。
    ''' </summary>
    Public Class SwcNode

        ''' <summary>
        ''' 节点编号 (``n``)，在同一个骨架文件之中唯一。
        ''' </summary>
        Public Property Id As Integer

        ''' <summary>
        ''' 节点类型编号 (``label``)，常见的取值定义参考 <see cref="SwcSkeleton.Labels"/>。
        ''' </summary>
        Public Property Label As Integer

        ''' <summary>
        ''' 节点坐标 X (按照文件原值保存，单位参考 <see cref="Unit"/>)。
        ''' </summary>
        Public Property X As Double

        ''' <summary>
        ''' 节点坐标 Y (按照文件原值保存，单位参考 <see cref="Unit"/>)。
        ''' </summary>
        Public Property Y As Double

        ''' <summary>
        ''' 节点坐标 Z (按照文件原值保存，单位参考 <see cref="Unit"/>)。
        ''' </summary>
        Public Property Z As Double

        ''' <summary>
        ''' 节点半径 (按照文件原值保存，单位参考 <see cref="Unit"/>)。
        ''' </summary>
        Public Property Radius As Double

        ''' <summary>
        ''' 父节点编号，根节点的值为 ``-1``。
        ''' </summary>
        Public Property Parent As Integer = -1

        ''' <summary>
        ''' 当前节点数据所使用的单位。
        ''' </summary>
        Public Property Unit As SwcUnits = SwcUnits.Nanometer

        ''' <summary>
        ''' 父节点对象，根节点为 Nothing。
        ''' </summary>
        Public Property ParentNode As SwcNode

        ''' <summary>
        ''' 当前节点的所有子节点。
        ''' </summary>
        Public ReadOnly Property Children As List(Of SwcNode) = New List(Of SwcNode)

        ''' <summary>
        ''' 当前节点是否是根节点？(``parent = -1``)
        ''' </summary>
        Public ReadOnly Property IsRoot As Boolean
            Get
                Return Parent < 0
            End Get
        End Property

        ''' <summary>
        ''' 子节点的数量。
        ''' </summary>
        Public ReadOnly Property Degree As Integer
            Get
                Return Children.Count
            End Get
        End Property

#Region "unit accessors"

        ''' <summary>
        ''' 坐标 X 的纳米值。
        ''' </summary>
        Public ReadOnly Property XNm As Double
            Get
                Return X.ToNanometer(Unit)
            End Get
        End Property

        ''' <summary>
        ''' 坐标 X 的微米值。
        ''' </summary>
        Public ReadOnly Property XMicrons As Double
            Get
                Return X.ToMicrometer(Unit)
            End Get
        End Property

        ''' <summary>
        ''' 坐标 Y 的纳米值。
        ''' </summary>
        Public ReadOnly Property YNm As Double
            Get
                Return Y.ToNanometer(Unit)
            End Get
        End Property

        ''' <summary>
        ''' 坐标 Y 的微米值。
        ''' </summary>
        Public ReadOnly Property YMicrons As Double
            Get
                Return Y.ToMicrometer(Unit)
            End Get
        End Property

        ''' <summary>
        ''' 坐标 Z 的纳米值。
        ''' </summary>
        Public ReadOnly Property ZNm As Double
            Get
                Return Z.ToNanometer(Unit)
            End Get
        End Property

        ''' <summary>
        ''' 坐标 Z 的微米值。
        ''' </summary>
        Public ReadOnly Property ZMicrons As Double
            Get
                Return Z.ToMicrometer(Unit)
            End Get
        End Property

        ''' <summary>
        ''' 半径的纳米值。
        ''' </summary>
        Public ReadOnly Property RadiusNm As Double
            Get
                Return Radius.ToNanometer(Unit)
            End Get
        End Property

        ''' <summary>
        ''' 半径的微米值。
        ''' </summary>
        Public ReadOnly Property RadiusMicrons As Double
            Get
                Return Radius.ToMicrometer(Unit)
            End Get
        End Property

#End Region

        ''' <summary>
        ''' 从当前节点指向父节点的空间距离 (按照当前节点的单位)。
        ''' </summary>
        ''' <returns>根节点 (没有父节点) 会返回 0。</returns>
        Public Function DistanceToParent() As Double
            If ParentNode Is Nothing Then
                Return 0
            End If

            Return DistanceTo(ParentNode)
        End Function

        ''' <summary>
        ''' 计算从当前节点到目标节点的欧几里得距离 (按照当前节点的单位)。
        ''' </summary>
        Public Function DistanceTo(other As SwcNode) As Double
            If other Is Nothing Then
                Return 0
            End If

            Dim dx As Double = X - other.X
            Dim dy As Double = Y - other.Y
            Dim dz As Double = Z - other.Z

            Return Math.Sqrt(dx * dx + dy * dy + dz * dz)
        End Function

        Public Overrides Function ToString() As String
            Return $"#{Id} [label={Label}] ({X}, {Y}, {Z}) r={Radius} parent={Parent}"
        End Function

    End Class

    ''' <summary>
    ''' 骨架的坐标包围盒 (按照骨架的单位)。
    ''' </summary>
    Public Class SwcBoundingBox

        Public Property MinX As Double
        Public Property MaxX As Double
        Public Property MinY As Double
        Public Property MaxY As Double
        Public Property MinZ As Double
        Public Property MaxZ As Double

        Public ReadOnly Property SizeX As Double
            Get
                Return MaxX - MinX
            End Get
        End Property

        Public ReadOnly Property SizeY As Double
            Get
                Return MaxY - MinY
            End Get
        End Property

        Public ReadOnly Property SizeZ As Double
            Get
                Return MaxZ - MinZ
            End Get
        End Property

        Public Overrides Function ToString() As String
            Return $"[{MinX}, {MaxX}] x [{MinY}, {MaxY}] x [{MinZ}, {MaxZ}]"
        End Function

    End Class
End Namespace
