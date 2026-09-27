Imports System.Math

''' <summary>可一键触发的预设动作。</summary>
Public Enum ActionPreset
    ''' <summary>原地站立（维持平衡）。</summary>
    Stand = 0
    ''' <summary>向前行走。</summary>
    Walk = 1
    ''' <summary>加速奔跑。</summary>
    Run = 2
    ''' <summary>向左转向（原地 + 行进间）。</summary>
    TurnLeft = 3
    ''' <summary>向右转向。</summary>
    TurnRight = 4
    ''' <summary>助跑起跳。</summary>
    Jump = 5
    ''' <summary>高抬腿跨越低矮障碍。</summary>
    StepOver = 6
    ''' <summary>攀爬台阶。</summary>
    ClimbStairs = 7
    ''' <summary>减速停住。</summary>
    Stop = 8
End Enum

''' <summary>自动演示脚本中的一步。</summary>
Public Class ScriptStep

    ''' <summary>本步执行的动作。</summary>
    Public Property Action As ActionPreset
    ''' <summary>最长持续时间（秒）。</summary>
    Public Property Duration As Double
    ''' <summary>
    ''' 可选的结束条件：火柴人沿 X 方向前进到该坐标即提前结束本步。
    ''' 用位置而不是时间来驱动，演示脚本在角色速度波动时依然可靠。
    ''' </summary>
    Public Property UntilX As Double? = Nothing

    ''' <summary>步的显示名。</summary>
    Public Property Title As String

    Sub New(action As ActionPreset, duration As Double, title As String, Optional untilX As Double? = Nothing)
        Me.Action = action
        Me.Duration = duration
        Me.Title = title
        Me.UntilX = untilX
    End Sub
End Class

''' <summary>
''' 动作调度器：维护当前动作、时限，以及可选的自动演示脚本。
''' </summary>
Public Class ActionDirector

    Private _current As ActionPreset = ActionPreset.Stand

    ''' <summary>当前动作。</summary>
    Public ReadOnly Property Current As ActionPreset
        Get
            Return _current
        End Get
    End Property

    ''' <summary>当前动作已经持续的时间。</summary>
    Public Property Elapsed As Double = 0.0

    ''' <summary>当前动作的时限（秒）；负数表示不限时。</summary>
    Public Property TimeLimit As Double = -1.0

    ''' <summary>时限结束后回退到的动作。</summary>
    Public Property Fallback As ActionPreset = ActionPreset.Walk

    Private script As List(Of ScriptStep) = Nothing
    Private scriptIndex As Integer = 0
    Private scriptElapsed As Double = 0.0

    ''' <summary>是否正在播放自动演示。</summary>
    Public ReadOnly Property DemoRunning As Boolean
        Get
            Return script IsNot Nothing AndAlso scriptIndex < script.Count
        End Get
    End Property

    ''' <summary>演示进度文本。</summary>
    Public ReadOnly Property StatusText As String
        Get
            If Not DemoRunning Then
                Return $"{ActionName(Current)}"
            End If

            Return $"[{scriptIndex + 1}/{script.Count}] {script(scriptIndex).Title} ({scriptElapsed:F1}s)"
        End Get
    End Property

    ''' <summary>
    ''' 切换动作。
    ''' </summary>
    ''' <param name="action">目标动作。</param>
    ''' <param name="duration">限时（秒）；到达后回退到 <paramref name="fallback"/>。负数表示不限时。</param>
    Public Sub SetAction(action As ActionPreset, Optional duration As Double = -1.0,
                         Optional fallback As ActionPreset = ActionPreset.Walk)
        _current = action
        TimeLimit = duration
        Fallback = fallback
        Elapsed = 0.0
    End Sub

    ''' <summary>开始播放自动演示脚本。</summary>
    Public Sub StartDemo(steps As List(Of ScriptStep))
        script = steps
        scriptIndex = 0
        scriptElapsed = 0.0

        If script IsNot Nothing AndAlso script.Count > 0 Then
            Call SetAction(script(0).Action, script(0).Duration, ActionPreset.Stand)
        End If
    End Sub

    ''' <summary>停止自动演示。</summary>
    Public Sub StopDemo()
        script = Nothing
        scriptIndex = 0
    End Sub

    ''' <summary>
    ''' 推进调度器。返回当前动作（可能已被切换）。
    ''' </summary>
    ''' <param name="dt">帧间隔。</param>
    ''' <param name="positionX">火柴人当前的 X 坐标，用于脚本的位置触发。</param>
    Public Function Update(dt As Double, positionX As Double) As ActionPreset
        Elapsed += dt

        If DemoRunning Then
            scriptElapsed += dt

            Dim step_ As ScriptStep = script(scriptIndex)
            Dim done As Boolean = (step_.Duration > 0 AndAlso scriptElapsed >= step_.Duration)

            If step_.UntilX.HasValue AndAlso positionX >= step_.UntilX.Value Then
                done = True
            End If

            If done Then
                scriptIndex += 1
                scriptElapsed = 0.0

                If scriptIndex < script.Count Then
                    Call SetAction(script(scriptIndex).Action, script(scriptIndex).Duration, ActionPreset.Stand)
                Else
                    Call SetAction(ActionPreset.Stand, -1.0, ActionPreset.Stand)
                End If
            End If

            Return _current
        End If

        If TimeLimit > 0 AndAlso Elapsed >= TimeLimit Then
            Call SetAction(Fallback, -1.0, Fallback)
        End If

        Return _current
    End Function

    ''' <summary>动作中文名。</summary>
    Public Shared Function ActionName(a As ActionPreset) As String
        Select Case a
            Case ActionPreset.Stand : Return "站立"
            Case ActionPreset.Walk : Return "行走"
            Case ActionPreset.Run : Return "奔跑"
            Case ActionPreset.TurnLeft : Return "左转"
            Case ActionPreset.TurnRight : Return "右转"
            Case ActionPreset.Jump : Return "跳跃"
            Case ActionPreset.StepOver : Return "跨越障碍"
            Case ActionPreset.ClimbStairs : Return "上台阶"
            Case ActionPreset.Stop : Return "停止"
            Case Else : Return a.ToString()
        End Select
    End Function

    ''' <summary>生成默认的演示脚本：站立 → 行走 → 跨越 → 行走 → 上台阶 → 转向 → 跳跃 → 站立。</summary>
    Public Shared Function DefaultScript() As List(Of ScriptStep)
        Return New List(Of ScriptStep) From {
            New ScriptStep(ActionPreset.Stand, 2.0, "起势站立"),
            New ScriptStep(ActionPreset.Walk, 14.0, "走向障碍物", 5.6),
            New ScriptStep(ActionPreset.StepOver, 4.0, "跨越低矮障碍", 10.2),
            New ScriptStep(ActionPreset.Walk, 12.0, "走向台阶", 12.4),
            New ScriptStep(ActionPreset.ClimbStairs, 12.0, "攀爬台阶", 18.4),
            New ScriptStep(ActionPreset.Walk, 6.0, "高台上行走"),
            New ScriptStep(ActionPreset.TurnLeft, 3.2, "原地左转 180°"),
            New ScriptStep(ActionPreset.Walk, 10.0, "折返行走", 12.4),
            New ScriptStep(ActionPreset.Jump, 2.0, "助跑起跳"),
            New ScriptStep(ActionPreset.Walk, 5.0, "落地继续行走"),
            New ScriptStep(ActionPreset.Stand, 3.0, "收势站立")
        }
    End Function
End Class
