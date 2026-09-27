Imports System.IO
Imports System.Math
Imports Microsoft.VisualBasic.Imaging.Physics
Imports std = System.Math

''' <summary>
''' 无头冒烟测试：不创建任何窗口，直接跑物理循环并输出量化指标。
''' </summary>
''' <remarks>
''' 命令行执行 <c>StickFigure.exe --smoke</c> 即可运行，结果写入
''' 程序目录下的 <c>smoke-report.txt</c>。
''' 这是调参（马达增益、平衡反射、步态幅度）的主要依据：
''' 看角色能不能站住、能不能真的往前走、会不会出现 NaN。
''' </remarks>
Public Module SmokeTest

    Private Class Metrics
        Public minHeadY As Double = Double.MaxValue
        Public maxTilt As Double = 0.0
        Public hasNaN As Boolean = False
        Public maxSpeed As Double = 0.0
        Public fallenFrames As Integer = 0

        Public Sub Update(env As FigureEnvironment)
            Dim head As Vec3 = env.Skeleton.HeadPosition

            If Double.IsNaN(head.X) OrElse Double.IsNaN(head.Y) OrElse Double.IsNaN(head.Z) Then
                hasNaN = True
                Return
            End If

            minHeadY = std.Min(minHeadY, head.Y)
            maxTilt = std.Max(maxTilt, env.Skeleton.TiltAngle)

            Dim v As Vec3 = Vec3.FromPhysics(env.Skeleton.Bodies(BoneIndex.Pelvis).Velocity)

            maxSpeed = std.Max(maxSpeed, v.Length)

            If env.Skeleton.IsFallen Then
                fallenFrames += 1
            End If
        End Sub
    End Class

    ''' <summary>
    ''' 诊断模式：逐帧推进直到某个刚体的速度超过阈值，打印全部刚体的状态，
    ''' 用于定位数值发散的来源（马达刚度 / 约束 / 平衡反射）。
    ''' </summary>
    Public Function Diagnose(Optional threshold As Double = 60.0, Optional maxFrames As Integer = 600) As Integer
        Dim log As New List(Of String)()
        Dim env As New FigureEnvironment()
        Dim dt As Double = 1.0 / 60.0

        env.Act(ActionPreset.Stand)

        For frame As Integer = 1 To maxFrames
            env.Step(dt)

            Dim worst As RigidBody3D = Nothing
            Dim worstV As Double = 0.0

            For Each b As RigidBody3D In env.Skeleton.AllBodies
                Dim v As Double = Vec3.FromPhysics(b.Velocity).Length
                Dim w As Double = Vec3.FromPhysics(b.AngularVelocity).Length

                If v + w > worstV Then
                    worstV = v + w
                    worst = b
                End If
            Next

            If frame <= 3 OrElse frame Mod 30 = 0 Then
                Call log.Add($"frame {frame}: worst={worst.Label} |v|={Vec3.FromPhysics(worst.Velocity).Length:F3} " &
                             $"|w|={Vec3.FromPhysics(worst.AngularVelocity).Length:F3} " &
                             $"pelvisY={env.Skeleton.PelvisPosition.Y:F3} " &
                             $"headY={env.Skeleton.HeadPosition.Y:F3} " &
                             $"tilt={env.Skeleton.TiltAngle * 180 / std.PI:F1}° " &
                             $"kneeL={env.Skeleton.Motors(JointIndex.KneeL).AngleError:F3} " &
                             $"hipL={env.Skeleton.Motors(JointIndex.HipL).AngleError:F3} " &
                             $"spine={env.Skeleton.Motors(JointIndex.Spine).AngleError:F3}")
            End If

            If worstV > threshold Then
                Call log.Add("")
                Call log.Add($"!!! 发散于 frame {frame} (t={frame * dt:F3}s)，阈值={threshold}")
                Call log.Add($"body  |  |v|  |  |omega|  |  position")

                For Each b As RigidBody3D In env.Skeleton.AllBodies
                    Call log.Add($"{b.Label,-12} {Vec3.FromPhysics(b.Velocity).Length,14:F3} " &
                                 $"{Vec3.FromPhysics(b.AngularVelocity).Length,14:F3}  {Vec3.FromPhysics(b.Position)}")
                Next

                Exit For
            End If
        Next

        Dim text As String = String.Join(vbCrLf, log)

        Call Console.WriteLine(text)

        Try
            Call File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "diagnose-report.txt"), text)
        Catch
        End Try

        Return 0
    End Function

    ''' <summary>运行冒烟测试，返回退出码（0 = 全部通过）。</summary>
    Public Function Run(Optional reportFile As String = Nothing) As Integer
        Dim log As New List(Of String)()
        Dim exitCode As Integer = 0

        Call log.Add("=== 3D 火柴人 无头冒烟测试 ===")
        Call log.Add($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")

        Dim env As New FigureEnvironment()
        Dim dt As Double = 1.0 / 60.0

        Call log.Add($"体重: {env.Skeleton.TotalMass:F2} kg   刚体: {env.Skeleton.Bodies.Count}   关节: {env.Skeleton.Joints.Count}")
        Call log.Add($"出生点: {env.Level.StartPosition}   朝向: {env.Level.StartHeading * 180 / std.PI:F1}°")

        ' ---------------- 阶段 1：站立平衡 ----------------
        Dim stand As New Metrics()

        env.Act(ActionPreset.Stand)

        For i As Integer = 1 To CInt(4.0 / dt)
            env.Step(dt)
            stand.Update(env)
        Next

        Call log.Add("")
        Call log.Add("[阶段1 站立 4s]")
        Call log.Add($"  头部最低高度 : {stand.minHeadY:F3} m   (期望 > 1.45)")
        Call log.Add($"  最大躯干倾角 : {stand.maxTilt * 180 / std.PI:F1}°   (期望 < 20°)")
        Call log.Add($"  最大速度     : {stand.maxSpeed:F3} m/s")
        Call log.Add($"  跌倒帧数     : {stand.fallenFrames}")
        Call log.Add($"  NaN          : {stand.hasNaN}")

        If stand.hasNaN Then
            exitCode = 1
            Call log.Add("  => 失败：出现 NaN")
        ElseIf stand.minHeadY < 1.45 Then
            exitCode = 1
            Call log.Add("  => 失败：站立时头部过低（角色瘫倒）")
        ElseIf stand.maxTilt > 20 * std.PI / 180 Then
            exitCode = 1
            Call log.Add("  => 失败：站立时躯干倾斜过大")
        Else
            Call log.Add("  => 通过")
        End If

        ' ---------------- 阶段 2：向前行走 ----------------
        Dim startX As Double = env.Position.X
        Dim walk As New Metrics()

        env.Act(ActionPreset.Walk)

        For i As Integer = 1 To CInt(8.0 / dt)
            env.Step(dt)
            walk.Update(env)

            If i Mod 60 = 0 Then
                Call log.Add($"  t={i * dt:F1}s  x={env.Position.X:F2}  z={env.Position.Z:F2}  " &
                             $"headY={env.Skeleton.HeadPosition.Y:F2}  tilt={env.Skeleton.TiltAngle * 180 / std.PI:F1}°  " &
                             $"speed={Vec3.FromPhysics(env.Skeleton.Bodies(BoneIndex.Pelvis).Velocity).Length:F2}")
            End If
        Next

        Dim travelled As Double = env.Position.X - startX

        Call log.Add("")
        Call log.Add("[阶段2 行走 8s]")
        Call log.Add($"  X 位移       : {travelled:F2} m   (期望 > 3.0)")
        Call log.Add($"  头部最低高度 : {walk.minHeadY:F3} m")
        Call log.Add($"  最大躯干倾角 : {walk.maxTilt * 180 / std.PI:F1}°")
        Call log.Add($"  跌倒帧数     : {walk.fallenFrames}")
        Call log.Add($"  NaN          : {walk.hasNaN}")

        If walk.hasNaN Then
            exitCode = 1
            Call log.Add("  => 失败：出现 NaN")
        ElseIf travelled < 3.0 Then
            exitCode = 1
            Call log.Add("  => 失败：行走位移不足")
        Else
            Call log.Add("  => 通过")
        End If

        ' ---------------- 阶段 3：跳跃 ----------------
        Dim beforeJump As Double = env.Skeleton.PelvisPosition.Y

        env.Act(ActionPreset.Jump)

        Dim peak As Double = beforeJump

        For i As Integer = 1 To CInt(1.5 / dt)
            env.Step(dt)
            peak = std.Max(peak, env.Skeleton.PelvisPosition.Y)
        Next

        Call log.Add("")
        Call log.Add("[阶段3 跳跃]")
        Call log.Add($"  起跳前骨盆高 : {beforeJump:F3} m")
        Call log.Add($"  最高骨盆高   : {peak:F3} m   (期望抬升 > 0.15)")

        If peak - beforeJump < 0.15 Then
            exitCode = 1
            Call log.Add("  => 失败：跳跃没有明显离地")
        Else
            Call log.Add("  => 通过")
        End If

        ' ---------------- 阶段 4：观测接口 ----------------
        Call env.Act(ActionPreset.Walk)

        For i As Integer = 1 To 10
            env.Step(dt)
        Next

        Dim obs = env.GetObservation(withVision:=False)
        Dim vec As Single() = obs.ToVector()

        Call log.Add("")
        Call log.Add("[阶段4 观测接口]")
        Call log.Add($"  观测向量维度 : {vec.Length}")
        Call log.Add($"  观测摘要     : {obs}")
        Call log.Add($"  关节局部坐标 : {obs.JointLocal.Length} 个分量")
        Call log.Add($"  足部接触     : [{obs.FootContact(0)}, {obs.FootContact(1)}]")

        If vec.Any(Function(x) Single.IsNaN(x)) Then
            exitCode = 1
            Call log.Add("  => 失败：观测向量含 NaN")
        Else
            Call log.Add("  => 通过")
        End If

        Call log.Add("")
        Call log.Add($"退出码: {exitCode}")

        Dim text As String = String.Join(vbCrLf, log)

        Call Console.WriteLine(text)

        If reportFile Is Nothing Then
            reportFile = Path.Combine(AppContext.BaseDirectory, "smoke-report.txt")
        End If

        Try
            Call File.WriteAllText(reportFile, text)
            Call Console.WriteLine($"报告已写入: {reportFile}")
        Catch ex As Exception
            Call Console.WriteLine($"写报告失败: {ex.Message}")
        End Try

        Return exitCode
    End Function
End Module
