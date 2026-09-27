Imports System.IO
Imports FlywireAI.Connectome
Imports FlywireAI.FAFBv783
Imports std = System.Math

''' <summary>
''' 果蝇大脑 ↔ 火柴人 的无头命令行入口（不创建任何窗口）。
''' </summary>
''' <remarks>
''' <code>
''' StickFigure.exe --brain-train                       装配大脑 + 模仿学习 + DAgger + 存权重
''' StickFigure.exe --brain-run                         装配大脑 + 加载权重 + 闭环跑一段并输出指标
''' 可选参数：
'''   --data    &lt;dir&gt;      FAFB v783 数据目录（默认取 SnnConfig 的默认值）
'''   --pack    &lt;file&gt;     msgpack 转储包（给了它就不再读 csv）
'''   --weights &lt;file&gt;     读出层权重的存读路径
'''   --episodes &lt;n&gt;       训练局数（默认 2）
'''   --ticks    &lt;n&gt;       每局的大脑 tick 数（默认 240）
'''   --dagger   &lt;n&gt;       DAgger 轮数（默认 1）
'''   --gpu                 启用 CUDA 后端（失败自动回退 CPU）
''' </code>
''' </remarks>
Public Module FlyBrainCli

    Private Function OptionValue(args As Collections.ObjectModel.ReadOnlyCollection(Of String),
                                 name As String) As String
        For i As Integer = 0 To args.Count - 2
            If args(i).Equals(name, StringComparison.OrdinalIgnoreCase) Then
                Return args(i + 1)
            End If
        Next

        Return Nothing
    End Function

    Private Function OptionInt(args As Collections.ObjectModel.ReadOnlyCollection(Of String),
                               name As String, fallback As Integer) As Integer
        Dim text As String = OptionValue(args, name)
        Dim value As Integer

        If text IsNot Nothing AndAlso Integer.TryParse(text, value) Then
            Return value
        End If

        Return fallback
    End Function

    Private Function HasFlag(args As Collections.ObjectModel.ReadOnlyCollection(Of String),
                             name As String) As Boolean
        Return args.Any(Function(a) a.Equals(name, StringComparison.OrdinalIgnoreCase))
    End Function

    ''' <summary>构建配置并检查数据是否可用；不可用时返回 Nothing 并写好错误信息。</summary>
    Private Function BuildConfig(args As Collections.ObjectModel.ReadOnlyCollection(Of String),
                                 ByRef pack As FafbPackReader,
                                 ByRef errorMessage As String) As SnnConfig
        pack = Nothing
        errorMessage = Nothing

        Dim config As New SnnConfig()
        Dim dataDir As String = OptionValue(args, "--data")
        Dim packFile As String = OptionValue(args, "--pack")

        If Not String.IsNullOrWhiteSpace(dataDir) Then
            config.DataDir = dataDir
        End If

        If Not String.IsNullOrWhiteSpace(packFile) Then
            config.PackFile = packFile
        End If

        config.UseGpu = HasFlag(args, "--gpu")
        config.Seed = 42

        ' 数据可用性检查：优先找 msgpack 转储包，找不到再找 csv
        Dim candidates As String() = FafbMsgPackStorage.CandidatePackPaths()

        If candidates IsNot Nothing AndAlso candidates.Length > 0 AndAlso File.Exists(candidates(0)) Then
            pack = FafbMsgPackStorage.Open(candidates(0))
            Return config
        End If

        If Not String.IsNullOrWhiteSpace(config.PackFile) AndAlso File.Exists(config.PackFile) Then
            pack = FafbMsgPackStorage.Open(config.PackFile)
            Return config
        End If

        If Directory.Exists(config.DataDir) AndAlso
           File.Exists(config.ResolvePath(config.ConnectionsCsv)) Then
            Return config
        End If

        errorMessage =
            "找不到果蝇全脑连接组数据。" & vbCrLf &
            $"  数据目录: {config.DataDir}" & vbCrLf &
            $"  期望文件: {config.ConnectionsCsv} / {config.NamesCsv} / {config.ClassificationCsv} ..." & vbCrLf &
            "  请用 --data <dir> 指定 FAFB v783 数据目录，或用 --pack <file> 指定 msgpack 转储包。"

        Return Nothing
    End Function

    Private Function MakeSession(args As Collections.ObjectModel.ReadOnlyCollection(Of String),
                                 config As SnnConfig, pack As FafbPackReader) As FlySession
        Call Console.WriteLine("装配果蝇全脑连接组（数十秒）...")

        Dim host As FlyBrainHost = FlyBrainHost.Create(config,
                                                       Sub(msg) Console.WriteLine("  " & msg), pack)
        Dim env As New FigureEnvironment()
        Dim session As New FlySession(env, host)
        Dim weights As String = OptionValue(args, "--weights")

        If Not String.IsNullOrWhiteSpace(weights) Then
            session.WeightsFile = weights
        End If

        Call Console.WriteLine($"大脑: {session.Brain.FlowSummary}")
        Call Console.WriteLine($"读出特征维: {session.Brain.MotorFeatures.Length}")

        Return session
    End Function

    ''' <summary>装配大脑 → 模仿学习 + DAgger → 保存权重。</summary>
    Public Function Train(args As Collections.ObjectModel.ReadOnlyCollection(Of String)) As Integer
        Dim pack As FafbPackReader = Nothing
        Dim errorMessage As String = Nothing
        Dim config As SnnConfig = BuildConfig(args, pack, errorMessage)

        If config Is Nothing Then
            Call Console.WriteLine(errorMessage)
            Return 2
        End If

        Dim episodes As Integer = OptionInt(args, "--episodes", 2)
        Dim ticks As Integer = OptionInt(args, "--ticks", 240)
        Dim dagger As Integer = OptionInt(args, "--dagger", 1)

        Console.WriteLine("=== 果蝇大脑驱动火柴人：模仿学习 ===")
        Console.WriteLine(config.Describe())

        Try
            Dim session As FlySession = MakeSession(args, config, pack)

            session.Reporter = Sub(msg) Console.WriteLine(msg)

            Console.WriteLine(session.Train(episodes, ticks, dagger))
            Call session.TryLoadWeights()

            Dim result = session.Evaluate(std.Min(600, ticks * 2))

            Console.WriteLine($"闭环评估: 位移={result.Travelled:F2} m, 跌倒帧={result.Falls}")

            Call session.Dispose()
        Catch ex As Exception
            Call Console.WriteLine("训练失败: " & ex.ToString())
            Return 2
        Finally
            If pack IsNot Nothing Then
                Call pack.Dispose()
            End If
        End Try

        Return 0
    End Function

    ''' <summary>装配大脑 → 加载权重 → 闭环运行并输出指标。</summary>
    Public Function Run(args As Collections.ObjectModel.ReadOnlyCollection(Of String)) As Integer
        Dim pack As FafbPackReader = Nothing
        Dim errorMessage As String = Nothing
        Dim config As SnnConfig = BuildConfig(args, pack, errorMessage)

        If config Is Nothing Then
            Call Console.WriteLine(errorMessage)
            Return 2
        End If

        Dim ticks As Integer = OptionInt(args, "--ticks", 600)

        Console.WriteLine("=== 果蝇大脑驱动火柴人：闭环运行 ===")
        Console.WriteLine(config.Describe())

        Try
            Dim session As FlySession = MakeSession(args, config, pack)
            Dim loaded As Boolean = session.TryLoadWeights()

            Console.WriteLine($"读出层权重: {If(loaded, "已加载 " & session.WeightsFile, "未找到（使用随机初始化）")}")
            Console.WriteLine($"闭环运行 {ticks} 大脑 tick（{ticks * session.TickDivisor} 物理步）...")

            Call session.Install()

            Dim dt As Double = 1.0 / 60.0
            Dim startX As Double = session.Environment.Position.X

            For i As Integer = 1 To ticks
                Call session.Environment.Step(dt)

                If i Mod 60 = 0 Then
                    Console.WriteLine($"  tick {i,4}: x={session.Environment.Position.X,8:F2}  " &
                                      $"headY={session.Environment.Skeleton.HeadPosition.Y,6:F2}  " &
                                      $"tilt={session.Environment.Skeleton.TiltAngle * 180 / std.PI,5:F1}°  " &
                                      $"spikes={session.Brain.MotorActiveSpikes,5:F0}  fallen={session.Environment.IsFallen}")
                End If
            Next

            Console.WriteLine($"结束: X 位移 = {session.Environment.Position.X - startX:F2} m, " &
                              $"位置 = ({session.Environment.Position.X:F2}, {session.Environment.Position.Z:F2})")

            Call session.Dispose()
        Catch ex As Exception
            Call Console.WriteLine("运行失败: " & ex.ToString())
            Return 2
        Finally
            If pack IsNot Nothing Then
                Call pack.Dispose()
            End If
        End Try

        Return 0
    End Function

End Module
