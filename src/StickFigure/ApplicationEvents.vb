Imports Microsoft.VisualBasic.ApplicationServices

Namespace My
    ' The following events are available for MyApplication:
    ' Startup: Raised when the application starts, before the startup form is created.
    ' Shutdown: Raised after all application forms are closed. This event is not raised if the application terminates abnormally.
    ' UnhandledException: Raised if the application encounters an unhandled exception.
    ' StartupNextInstance: Raised when launching a single-instance application and if it is already active.
    ' NetworkAvailabilityChanged: Raised when the network connection is connected or disconnected.

    ' **NEW** ApplyApplicationDefaults: Raised when the application queries default values to be set for the application.
    '
    ' Example:
    ' Private Sub MyApplication_ApplyApplicationDefaults(sender As Object, e As ApplyApplicationDefaultsEventArgs) Handles Me.ApplyApplicationDefaults
    '
    '   ' Setting the application-wide default Font:
    '   e.Font = New Font(FontFamily.GenericSansSerif, 12, FontStyle.Regular)
    '
    '   ' Setting the HighDpiMode for the application:
    '   e.HighDpiMode = HighDpiMode.PerPointPixelDensity
    '
    ' End Sub

    Partial Friend Class MyApplication

        ''' <summary>
        ''' 命令行入口：<c>--smoke</c> 运行无头物理冒烟测试（不创建任何窗口），
        ''' 结果写入程序目录下的 <c>smoke-report.txt</c>；其余情况正常启动主窗体。
        ''' </summary>
        Private Sub MyApplication_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
            Dim args As Collections.ObjectModel.ReadOnlyCollection(Of String) = e.CommandLine

            Dim smoke As Boolean = args.Any(Function(a) a = "--smoke" OrElse a = "-smoke")
            Dim diagnose As Boolean = args.Any(Function(a) a = "--diagnose" OrElse a = "-diagnose")
            Dim ablation As Boolean = args.Any(Function(a) a = "--ablation" OrElse a = "-ablation")
            Dim vision As Boolean = args.Any(Function(a) a = "--vision" OrElse a = "-vision")
            Dim demo As Boolean = args.Any(Function(a) a = "--demo" OrElse a = "-demo")

            If smoke OrElse diagnose OrElse ablation OrElse vision OrElse demo Then
                Dim code As Integer = 0

                Try
                    If diagnose Then
                        code = SmokeTest.Diagnose()
                    ElseIf ablation Then
                        code = SmokeTest.Ablation()
                    ElseIf vision Then
                        code = SmokeTest.VisionTest()
                    ElseIf demo Then
                        code = SmokeTest.Demo()
                    Else
                        code = SmokeTest.Run()
                    End If
                Catch ex As Exception
                    Call Console.WriteLine(ex.ToString())
                    code = 2
                End Try

                Environment.ExitCode = code
                e.Cancel = True
            End If
        End Sub

    End Class
End Namespace
