Imports System.Globalization
Imports Microsoft.VisualBasic.Scripting.Runtime

Namespace FAFBv783

    ''' <summary>
    ''' FlyWire 数据表格之中的 ID 列 (``root_id``, ``supervoxel_id`` 等) 的精确 Int64 解析器。
    ''' 
    ''' 表格框架默认的字符串转为 <see cref="Long"/> 的转换会先把文本解析为 <see cref="Double"/>，
    ''' 然后再转换为 <see cref="Long"/>；因为 <see cref="Double"/> 只能够精确表示 2^53 以内的整数，
    ''' 所以对于 FlyWire 的 root_id (大约为 7.2e17) 会发生精度丢失，例如:
    ''' 
    ''' ```
    ''' 720575940599457990 -> 720575940599458048
    ''' 720575940596125868 -> 720575940596125696
    ''' ```
    ''' 
    ''' 所以需要通过 <see cref="IParser"/> 自定义解析器直接从文本解析 Int64 数值，
    ''' 从而保证 ID 数据的精度。
    ''' </summary>
    Public Class Int64Parser : Implements IParser

        ''' <summary>
        ''' 从 csv 单元格的文本之中解析出 Int64 数值；空值或者无法解析的文本会返回 0。
        ''' </summary>
        Public Function TryParse(content As String) As Object Implements IParser.TryParse
            Dim text As String = If(content, "").Trim

            If text.Length = 0 Then
                Return 0L
            End If

            Dim value As Long

            ' 优先按照整数文本进行解析，避免 Double 转换所引入的精度丢失
            If Long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, value) Then
                Return value
            End If

            Dim numeric As Double

            ' 兼容科学计数法 (例如 7.2057594E+17) 等文本
            If Double.TryParse(text, NumberStyles.Float Or NumberStyles.AllowThousands, CultureInfo.InvariantCulture, numeric) Then
                Return CLng(numeric)
            End If

            Return 0L
        End Function

        ''' <summary>
        ''' 将 Int64 数值序列化为 csv 单元格的文本。
        ''' </summary>
        Public Overloads Function ToString(obj As Object) As String Implements IParser.ToString
            If obj Is Nothing Then
                Return ""
            End If

            Return obj.ToString
        End Function

    End Class
End Namespace
