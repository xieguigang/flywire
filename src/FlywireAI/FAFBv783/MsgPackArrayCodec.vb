Imports System.IO

Namespace FAFBv783

    ''' <summary>
    ''' 定型数值数组的 msgpack <b>快通道</b>：写出来的仍然是标准 msgpack，只是不走反射序列化器。
    ''' </summary>
    ''' <remarks>
    ''' <b>为什么要自己读写一遍</b>：<see cref="Microsoft.VisualBasic.Data.IO.MessagePack.MsgPackSerializer"/>
    ''' 对数组的反序列化是"逐元素"的 —— 每个值都要读一个格式头字节、<b>分配一个临时字节数组</b>、
    ''' 再 <c>Convert.ChangeType</c> 装箱一次。实测：
    ''' 
    ''' * 534 万行连接表 (2,670 万个值)：反射序列化器 3.1 s，直接读 csv 只要 1.7 s；
    ''' * 脑区表的 158 列 (2,140 万个值)：2.9 s，而只读需要的那些列再解析 csv 只要 0.85 s。
    ''' 
    ''' 也就是说"转成二进制"这件事本身没有省下时间，全被逐元素的分配开销吃掉了。
    ''' 这里改成：整段读进内存后按<b>大端序</b>直接搬字节（复用同一个 8 字节暂存区，零分配），
    ''' 每个值约 10 ns，比逐元素路径快一个数量级。
    ''' 
    ''' <b>格式</b>：``ARRAY_32 (0xDD) + 大端 uint32 元素个数``，后接
    ''' <c>FLOAT_64 (0xCB)</c> / <c>INT_64 (0xD3)</c> / <c>INT_32 (0xD2)</c> 定长元素 ——
    ''' 全部是标准 msgpack，别的 msgpack 实现也能读。
    ''' 
    ''' 只用于转储包里的"大数值列"（脑区表的列、连接表的列），
    ''' 元数据与字符串表仍然走 <see cref="Microsoft.VisualBasic.Data.IO.MessagePack.MsgPackSerializer"/>。
    ''' </remarks>
    Friend Module MsgPackArrayCodec

        Private Const Array32 As Byte = &HDD
        Private Const Float64 As Byte = &HCB
        Private Const Int64 As Byte = &HD3
        Private Const Int32 As Byte = &HD2

        ''' <summary>一次刷盘的元素个数（限制写缓冲的大小：1M × 9 B ≈ 9 MB）。</summary>
        Private Const ChunkSize As Integer = 1 << 20

#Region "写"

        Friend Sub WriteDoubles(stream As Stream, values As Double())
            If values Is Nothing OrElse values.Length = 0 Then
                Call writeEmpty(stream)

                Return
            End If

            Call writeHeader(stream, values.Length)

            Dim raw(7) As Byte

            For start As Integer = 0 To values.Length - 1 Step ChunkSize
                Dim finish As Integer = Math.Min(values.Length, start + ChunkSize) - 1
                Dim buffer As Byte() = New Byte((finish - start + 1) * 9 - 1) {}
                Dim at As Integer = 0

                For i As Integer = start To finish
                    buffer(at) = Float64
                    at += 1

                    Call copyOf(BitConverter.GetBytes(values(i)), raw, buffer, at)

                    at += 8
                Next

                Call stream.Write(buffer, 0, buffer.Length)
            Next

            Call stream.Flush()
        End Sub

        Friend Sub WriteLongs(stream As Stream, values As Long())
            If values Is Nothing OrElse values.Length = 0 Then
                Call writeEmpty(stream)

                Return
            End If

            Call writeHeader(stream, values.Length)

            Dim raw(7) As Byte

            For start As Integer = 0 To values.Length - 1 Step ChunkSize
                Dim finish As Integer = Math.Min(values.Length, start + ChunkSize) - 1
                Dim buffer As Byte() = New Byte((finish - start + 1) * 9 - 1) {}
                Dim at As Integer = 0

                For i As Integer = start To finish
                    buffer(at) = Int64
                    at += 1

                    Call copyOf(BitConverter.GetBytes(values(i)), raw, buffer, at)

                    at += 8
                Next

                Call stream.Write(buffer, 0, buffer.Length)
            Next

            Call stream.Flush()
        End Sub

        Friend Sub WriteIntegers(stream As Stream, values As Integer())
            If values Is Nothing OrElse values.Length = 0 Then
                Call writeEmpty(stream)

                Return
            End If

            Call writeHeader(stream, values.Length)

            Dim raw(3) As Byte

            For start As Integer = 0 To values.Length - 1 Step ChunkSize
                Dim finish As Integer = Math.Min(values.Length, start + ChunkSize) - 1
                Dim buffer As Byte() = New Byte((finish - start + 1) * 5 - 1) {}
                Dim at As Integer = 0

                For i As Integer = start To finish
                    buffer(at) = Int32
                    at += 1

                    Call copyOf(BitConverter.GetBytes(values(i)), raw, buffer, at)

                    at += 4
                Next

                Call stream.Write(buffer, 0, buffer.Length)
            Next

            Call stream.Flush()
        End Sub

        ''' <summary>把小端字节翻转成大端（msgpack 是大端序），写进目标缓冲。</summary>
        Private Sub copyOf(source As Byte(), scratch As Byte(), target As Byte(), offset As Integer)
            Call Array.Copy(source, scratch, scratch.Length)
            Call Array.Reverse(scratch)
            Call Array.Copy(scratch, 0, target, offset, scratch.Length)
        End Sub

        Private Sub writeHeader(stream As Stream, count As Integer)
            stream.WriteByte(Array32)
            Call stream.Write(reversed(BitConverter.GetBytes(count)), 0, 4)
        End Sub

        Private Sub writeEmpty(stream As Stream)
            Call writeHeader(stream, 0)
            Call stream.Flush()
        End Sub

        Private Function reversed(data As Byte()) As Byte()
            Call Array.Reverse(data)

            Return data
        End Function

#End Region

#Region "读"

        Friend Function ReadDoubles(stream As Stream) As Double()
            Dim bytes As Byte() = readAll(stream)
            Dim at As Integer = 0
            Dim count As Integer = readHeader(bytes, at)
            Dim values As Double() = New Double(count - 1) {}
            Dim raw(7) As Byte

            For i As Integer = 0 To count - 1
                If bytes(at) <> Float64 Then
                    Throw New InvalidDataException($"msgpack 快通道：第 {i} 个元素不是 FLOAT_64 (0x{bytes(at):X2})")
                End If

                at += 1

                Call Array.Copy(bytes, at, raw, 0, 8)
                Call Array.Reverse(raw)

                values(i) = BitConverter.ToDouble(raw, 0)

                at += 8
            Next

            Return values
        End Function

        Friend Function ReadLongs(stream As Stream) As Long()
            Dim bytes As Byte() = readAll(stream)
            Dim at As Integer = 0
            Dim count As Integer = readHeader(bytes, at)
            Dim values As Long() = New Long(count - 1) {}
            Dim raw(7) As Byte

            For i As Integer = 0 To count - 1
                If bytes(at) <> Int64 Then
                    Throw New InvalidDataException($"msgpack 快通道：第 {i} 个元素不是 INT_64 (0x{bytes(at):X2})")
                End If

                at += 1

                Call Array.Copy(bytes, at, raw, 0, 8)
                Call Array.Reverse(raw)

                values(i) = BitConverter.ToInt64(raw, 0)

                at += 8
            Next

            Return values
        End Function

        Friend Function ReadIntegers(stream As Stream) As Integer()
            Dim bytes As Byte() = readAll(stream)
            Dim at As Integer = 0
            Dim count As Integer = readHeader(bytes, at)
            Dim values As Integer() = New Integer(count - 1) {}
            Dim raw(3) As Byte

            For i As Integer = 0 To count - 1
                If bytes(at) <> Int32 Then
                    Throw New InvalidDataException($"msgpack 快通道：第 {i} 个元素不是 INT_32 (0x{bytes(at):X2})")
                End If

                at += 1

                Call Array.Copy(bytes, at, raw, 0, 4)
                Call Array.Reverse(raw)

                values(i) = BitConverter.ToInt32(raw, 0)

                at += 4
            Next

            Return values
        End Function

        ''' <summary>读出 ``ARRAY_32 + 大端 uint32 个数``，并把游标推到第一个元素上。</summary>
        Private Function readHeader(bytes As Byte(), ByRef at As Integer) As Integer
            If bytes Is Nothing OrElse bytes.Length < 5 Then
                Throw New InvalidDataException("msgpack 快通道：数据太短，连数组头都不完整")
            End If

            If bytes(at) <> Array32 Then
                Throw New InvalidDataException($"msgpack 快通道：期望 ARRAY_32 (0xDD)，实际是 0x{bytes(at):X2}")
            End If

            at += 1

            Dim raw(3) As Byte

            Call Array.Copy(bytes, at, raw, 0, 4)
            Call Array.Reverse(raw)

            at += 4

            Return BitConverter.ToInt32(raw, 0)
        End Function

        Private Function readAll(stream As Stream) As Byte()
            Using buffer As New MemoryStream()
                Call stream.CopyTo(buffer)
                Call buffer.Flush()

                Return buffer.ToArray()
            End Using
        End Function

#End Region

    End Module

End Namespace
