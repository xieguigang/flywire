
''' <summary>
''' 竖排条形读数控件：一行一个通道，画名称 + 条形 + 数值。
''' </summary>
''' <remarks>
''' 手写而不是用 ProgressBar：一次要显示 16 行，ProgressBar 无法紧凑排布，
''' 也没法在同一行里带标签。
''' </remarks>
Public Class ChannelBars
    Inherits Control

    Private m_names As String() = New String() {}
    Private m_values As Double() = New Double() {}
    Private m_color As Color = Color.Cyan
    Private m_maximum As Double = 1.0

    ''' <summary>量程是否是自适应（<see cref="m_maximum"/> 传 &lt;= 0 时开启）。</summary>
    Private m_adaptive As Boolean

    ''' <summary>自适应量程的回落速度：每帧只允许缩到这个比例，避免读数一起一伏地跳。</summary>
    Private Const AdaptiveDecay As Double = 0.97

    ''' <summary>
    ''' 灌入一行行读数。
    ''' </summary>
    ''' <param name="maximum">
    ''' 量程。传 &lt;= 0 表示自适应：峰值立刻抬到最高，之后按 <see cref="AdaptiveDecay"/> 缓慢回落。
    ''' 适合"数量级事先不知道"的读数（例如每 tick 的脉冲个数）。
    ''' </param>
    Public Sub SetData(names As String(), values As Double(), color As Color, Optional maximum As Double = 0)
        m_names = If(names, New String() {})
        m_values = If(values, New Double() {})
        m_color = color
        m_adaptive = maximum <= 0

        If m_adaptive Then
            Dim peak As Double = 0

            For i As Integer = 0 To m_values.Length - 1
                Dim magnitude As Double = Math.Abs(m_values(i))

                If magnitude > peak Then peak = magnitude
            Next

            ' 峰值立即跟上（否则强读数会顶格看不出差别），之后缓慢回落
            m_maximum = If(peak >= m_maximum, peak, m_maximum * AdaptiveDecay)

            If m_maximum <= 0 Then m_maximum = 1.0
        Else
            m_maximum = maximum
        End If

        Call Invalidate()
    End Sub

    Public Sub New()
        Me.DoubleBuffered = True
        Me.BackColor = Color.FromArgb(20, 28, 38)
        Me.ForeColor = Color.FromArgb(203, 213, 225)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        Dim rowHeight As Single = Math.Max(12.0F, Me.ClientSize.Height / Math.Max(1, m_names.Length))

        Using font As New Font("Consolas", 8.0F)
            For i As Integer = 0 To m_names.Length - 1
                Dim y As Single = i * rowHeight
                Dim value As Double = If(i < m_values.Length, m_values(i), 0)
                Dim ratio As Double = Math.Min(1.0, Math.Abs(value) / m_maximum)
                Dim labelWidth As Single = 96

                Using brush As New SolidBrush(Me.ForeColor)
                    Call g.DrawString(m_names(i), font, brush, 2, y + 1)
                End Using

                Using brush As New SolidBrush(m_color)
                    Call g.FillRectangle(brush, labelWidth, y + 2, CSng((Me.ClientSize.Width - labelWidth - 40) * ratio),
                                         Math.Max(4.0F, rowHeight - 5))
                End Using

                Using brush As New SolidBrush(Me.ForeColor)
                    ' 自适应量程下的读数是"个脉冲"，F2 那串小数会画不下
                    Dim text As String = If(m_adaptive, value.ToString("F0"), value.ToString("F2"))

                    Call g.DrawString(text, font, brush, Me.ClientSize.Width - 38, y + 1)
                End Using
            Next
        End Using
    End Sub
End Class
