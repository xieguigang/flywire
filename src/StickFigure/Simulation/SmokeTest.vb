Imports System.IO
Imports System.Math
Imports Microsoft.VisualBasic.Imaging.Physics
Imports ImagingBitmap = Microsoft.VisualBasic.Imaging.Bitmap
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

        Dim useWalk As Boolean = Environment.GetCommandLineArgs().Any(Function(a) a = "--diagnose-walk")

        env.Act(If(useWalk, ActionPreset.Walk, ActionPreset.Stand))

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

    ''' <summary>
    ''' 消融实验：逐个关掉平衡反射，看哪个在破坏站立姿态。
    ''' </summary>
    Public Function Ablation(Optional seconds As Double = 3.0) As Integer
        Dim log As New List(Of String)()
        Dim dt As Double = 1.0 / 60.0

        Call log.Add("=== 平衡反射消融实验（站立 3s）===")
        Call log.Add("配置".PadRight(24) &
                     "最低头高".PadLeft(10) &
                     "最大倾角".PadLeft(10) &
                     "跌倒帧".PadLeft(8) &
                     "峰值|w|".PadLeft(10))

        Dim configs As (name As String, com As Boolean, height As Boolean, upright As Boolean,
                        heading As Boolean, motor As Double)() = {
            ("全部开启", True, True, True, True, 1.0),
            ("马达关闭", True, True, True, True, 0.0),
            ("马达 x0.15", True, True, True, True, 0.15),
            ("马达 x0.4", True, True, True, True, 0.4),
            ("马达 x1 无质心回中", False, True, True, True, 1.0),
            ("马达 x0.15 无质心回中", False, True, True, True, 0.15),
            ("马达 x0.15 无躯干直立", True, True, False, True, 0.15),
            ("马达 x0.15 全部平衡关闭", False, False, False, False, 0.15),
            ("马达关闭 全部平衡关闭", False, False, False, False, 0.0)
        }

        For Each cfg In configs
            Dim env As New FigureEnvironment()

            env.Balance.EnableComBalance = cfg.com
            env.Balance.HeightMaxAccel = If(cfg.height, 20.0, 0.0)
            env.Balance.UprightMaxAlpha = If(cfg.upright, 400.0, 0.0)
            env.Balance.HeadingMaxAlpha = If(cfg.heading, 120.0, 0.0)

            Call env.Skeleton.ApplyMotorGain(cfg.motor)
            env.Act(ActionPreset.Stand)

            Dim minHead As Double = Double.MaxValue
            Dim maxTilt As Double = 0.0
            Dim fallen As Integer = 0
            Dim peak As Double = 0.0

            For i As Integer = 1 To CInt(seconds / dt)
                env.Step(dt)

                minHead = std.Min(minHead, env.Skeleton.HeadPosition.Y)
                maxTilt = std.Max(maxTilt, env.Skeleton.TiltAngle)

                For Each b In env.Skeleton.AllBodies
                    peak = std.Max(peak, Vec3.FromPhysics(b.AngularVelocity).Length)
                Next

                If env.Skeleton.IsFallen Then fallen += 1
            Next

            Call log.Add(cfg.name.PadRight(24) &
                         minHead.ToString("F3").PadLeft(10) &
                         (maxTilt * 180 / std.PI).ToString("F1").PadLeft(10) &
                         fallen.ToString().PadLeft(8) &
                         peak.ToString("F1").PadLeft(10))
        Next

        Dim text As String = String.Join(vbCrLf, log)

        Call Console.WriteLine(text)

        Try
            Call File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ablation-report.txt"), text)
        Catch
        End Try

        Return 0
    End Function

    ''' <summary>
    ''' 离屏视觉取帧测试：验证头部视角的 <see cref="HeadViewRenderer"/> 能在
    ''' 没有任何窗口的情况下渲染并回读像素（神经网络输入的关键路径）。
    ''' </summary>
    Public Function VisionTest() As Integer
        Dim log As New List(Of String)()
        Dim exitCode As Integer = 0

        Call log.Add("=== 头部视角离屏取帧测试 ===")

        Dim env As New FigureEnvironment()
        Dim dt As Double = 1.0 / 60.0

        env.Act(ActionPreset.Walk)

        For i As Integer = 1 To 60
            env.Step(dt)
        Next

        Dim watch As Stopwatch = Stopwatch.StartNew()
        Dim frame As ImagingBitmap = env.GetVisionFrame(320, 240)

        watch.Stop()

        If frame Is Nothing Then
            exitCode = 1
            Call log.Add("失败：GetVisionFrame 返回 Nothing")
        Else
            Call log.Add($"GetVisionFrame(320,240) => {frame.Width}x{frame.Height}  耗时 {watch.Elapsed.TotalMilliseconds:F1} ms")

            Dim gray As Byte() = env.GetVisionGray(84, 84)
            Dim sum As Double = 0.0
            Dim distinct As New HashSet(Of Byte)()

            For Each b As Byte In gray
                sum += b
                Call distinct.Add(b)
            Next

            Call log.Add($"GetVisionGray(84,84) => {gray.Length} 字节  平均 {sum / gray.Length:F1}  灰阶数 {distinct.Count}")

            If distinct.Count <= 1 Then
                exitCode = 1
                Call log.Add("失败：画面是纯色（没有渲染出任何几何）")
            Else
                Call log.Add("=> 通过")
            End If

            Dim png As String = Path.Combine(AppContext.BaseDirectory, "head-view.png")

            Try
                ' ImagingBitmap 的缓冲是 BGRA，这里显式拷进 GDI+ 位图再存 PNG，
                ' 避免不同编码器的通道顺序差异干扰目检
                Dim buffer As Byte() = frame.MemoryBuffer.RawBuffer
                Dim w As Integer = frame.Width
                Dim h As Integer = frame.Height

                Using bmp As New Drawing.Bitmap(w, h, Drawing.Imaging.PixelFormat.Format32bppArgb)
                    Dim rect As New Drawing.Rectangle(0, 0, w, h)
                    Dim data As Drawing.Imaging.BitmapData = bmp.LockBits(rect, Drawing.Imaging.ImageLockMode.WriteOnly, Drawing.Imaging.PixelFormat.Format32bppArgb)

                    Call Runtime.InteropServices.Marshal.Copy(buffer, 0, data.Scan0, w * h * 4)
                    Call bmp.UnlockBits(data)

                    Call bmp.Save(png, Drawing.Imaging.ImageFormat.Png)
                End Using

                Call log.Add($"画面已保存: {png}")
            Catch ex As Exception
                Call log.Add($"保存失败: {ex.Message}")
            End Try
        End If

        Dim text As String = String.Join(vbCrLf, log)

        Call Console.WriteLine(text)

        Try
            Call File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "vision-report.txt"), text)
        Catch
        End Try

        Return exitCode
    End Function

    ''' <summary>
    ''' 无头回放自动演示脚本，逐 0.5s 打印动作 / 位置 / 姿态，
    ''' 用于定位"火柴人没跨过障碍"这类时序问题。
    ''' </summary>
    Public Function Demo(Optional seconds As Double = 40.0) As Integer
        Dim log As New List(Of String)()
        Dim env As New FigureEnvironment()
        Dim dt As Double = 1.0 / 60.0

        Call log.Add("=== 自动演示脚本 无头回放 ===")
        Call log.Add($"障碍: x={env.Level.ObstacleX} 高={env.Level.ObstacleHeight}   台阶: x={env.Level.StairsStartX}")
        Call log.Add($"{"t",6} {"动作",-12} {"x",8} {"z",8} {"pelvisY",8} {"headY",8} {"速度",7} {"触地"}")

        Call env.Director.StartDemo(ActionDirector.DefaultScript())

        Dim frames As Integer = CInt(seconds / dt)
        Dim obstacleCrossed As Boolean = False

        For i As Integer = 1 To frames
            env.Step(dt)

            If i Mod 30 = 0 Then
                Dim c As (Left As Boolean, Right As Boolean) = env.Skeleton.FootContact

                Call log.Add($"{env.Time,6:F1} {ActionDirector.ActionName(env.CurrentAction),-12} " &
                             $"{env.Position.X,8:F2} {env.Position.Z,8:F2} " &
                             $"{env.Skeleton.PelvisPosition.Y,8:F3} {env.Skeleton.HeadPosition.Y,8:F3} " &
                             $"{Vec3.FromPhysics(env.Skeleton.Bodies(BoneIndex.Pelvis).Velocity).Length,7:F2} " &
                             $"{If(c.Left, "L", "-")}{If(c.Right, "R", "-")}  " &
                             $"tilt={env.Skeleton.TiltAngle * 180 / std.PI,5:F1}° " &
                             $"kneeL={env.Skeleton.Motors(JointIndex.KneeL).AngleError,6:F2} " &
                             $"hipL={env.Skeleton.Motors(JointIndex.HipL).AngleError,6:F2} " &
                             $"spine={env.Skeleton.Motors(JointIndex.Spine).AngleError,6:F2} " &
                             $"want={env.Pose.TargetSpeed,5:F2} head={env.Gait.Heading * 180 / std.PI,4:F0}° " &
                             $"fwd=({env.Skeleton.BodyForward.X,5:F2},{env.Skeleton.BodyForward.Z,5:F2})")
            End If

            ' 障碍远沿在 ObstacleX + 厚度/2，再留 0.4m 余量判定"已经越过去"
            If Not obstacleCrossed AndAlso env.Position.X > env.Level.ObstacleX + 0.6 Then
                obstacleCrossed = True
                Call log.Add($">>> 在 t={env.Time:F1}s 越过障碍 (x={env.Position.X:F2})")
            End If
        Next

        Call log.Add("")
        Call log.Add($"越过障碍: {obstacleCrossed}   最终位置: ({env.Position.X:F2}, {env.Position.Z:F2})")

        Dim text As String = String.Join(vbCrLf, log)

        Call Console.WriteLine(text)

        Try
            Call File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "demo-report.txt"), text)
        Catch
        End Try

        Return If(obstacleCrossed, 0, 1)
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
        Call log.Add($"步态朝向: {env.Gait.Heading * 180 / std.PI:F1}°   躯干前向: {env.Skeleton.BodyForward}")

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
                             $"pelvisY={env.Skeleton.PelvisPosition.Y:F2}  headY={env.Skeleton.HeadPosition.Y:F2}  " &
                             $"tilt={env.Skeleton.TiltAngle * 180 / std.PI:F1}°  " &
                             $"speed={Vec3.FromPhysics(env.Skeleton.Bodies(BoneIndex.Pelvis).Velocity).Length:F2}  " &
                             $"heading={env.Gait.Heading * 180 / std.PI:F0}°  " &
                             $"fwd=({env.Skeleton.BodyForward.X:F2},{env.Skeleton.BodyForward.Z:F2})")
            End If
        Next

        Dim travelled As Double = env.Position.X - startX

        Call log.Add("")
        Call log.Add("[阶段2 行走 8s]")
        Call log.Add($"  X 位移       : {travelled:F2} m   (期望 > 3.0)")
        Call log.Add($"  平均足部接触 : {env.Skeleton.FootContact.Left Or env.Skeleton.FootContact.Right} (行走中至少有一只脚着地)")
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

            If i Mod 15 = 0 Then
                Dim pv As Vec3 = Vec3.FromPhysics(env.Skeleton.Bodies(BoneIndex.Pelvis).Velocity)
                Dim pf As Vector3 = env.Skeleton.Bodies(BoneIndex.Pelvis).ForceAccumulator

                Call log.Add($"  跳跃 t={i * dt:F2}s  pelvisY={env.Skeleton.PelvisPosition.Y:F3}  " &
                             $"vy={pv.Y:F2}  v=({pv.X:F2},{pv.Y:F2},{pv.Z:F2})  " &
                             $"F=({pf.x:F1},{pf.y:F1},{pf.z:F1})  " &
                             $"contact={env.Skeleton.FootContact.Left}/{env.Skeleton.FootContact.Right}")
            End If
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
