Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.VisualBasic.Drawing.DirectX
Imports Microsoft.VisualBasic.Drawing.DirectX.Scene3D
Imports std = System.Math

''' <summary>
''' 3D 火柴人仿真 demo 的主界面：
''' 左侧是可自由旋转/平移/缩放的<strong>主视角</strong>（<see cref="DxScene3DCanvas"/>），
''' 右侧上方是<strong>火柴人头部第一人称视角</strong>（同时是神经网络输入源），
''' 右侧下方是动作 / 参数控制面板与状态栏。
''' </summary>
''' <remarks>
''' 所有子控件都在代码里构建，设计器文件保持最小化。
''' 渲染循环用 <see cref="Timer"/> 驱动：每个 Tick 推进一次物理，然后分别
''' Invalidate 两个画布（必须 Invalidate 控件本身，Invalidate 父窗体不会重绘子画布）。
''' </remarks>
Public Class FigureCanvas

    ''' <summary>仿真环境（无头，可直接用于训练）。</summary>
    Private ReadOnly env As New FigureEnvironment()

    ''' <summary>主视角封装。</summary>
    Private ReadOnly orbit As New OrbitView()

    ''' <summary>头部视角封装（渲染器本身不是控件）。</summary>
    Private ReadOnly headView As New HeadViewRenderer()
    Private ReadOnly headCanvas As New DxCanvas()

    Private ReadOnly layout As New SplitContainer()
    Private ReadOnly rightColumn As New Panel()
    Private ReadOnly controlsPanel As New FlowLayoutPanel()
    Private ReadOnly statusBar As New Panel()
    Private ReadOnly clock As New Timer()

    ' 以下控件在 Build* 里由工厂方法创建后回填，因此不能声明成 ReadOnly
    Private btnDemo As Button
    Private trkSpeed As TrackBar
    Private trkStiffness As TrackBar
    Private chkFollow As CheckBox
    Private chkJoints As CheckBox
    Private chkGrid As CheckBox
    Private chkSelfBody As CheckBox

    ' ---- 果蝇大脑 ----
    Private chkBrain As CheckBox
    Private btnBrainTrain As Button
    Private lblBrainState As Label

    Private flyHost As FlyBrainHost
    Private flySession As FlySession
    Private brainLoading As Boolean
    Private brainTraining As Boolean

    Private lblTitle As Label
    Private lblAction As Label
    Private lblTime As Label
    Private lblPose As Label
    Private lblContact As Label
    Private lblFps As Label
    Private lblFallen As Label
    Private lblAgent As Label

    Private ReadOnly watch As New Diagnostics.Stopwatch()
    Private fps As Double = 0.0
    Private lastFrameMs As Double = 0.0

    Public Sub New()
        ' 此调用是设计器所必需的
        InitializeComponent()

        DoubleBuffered = True
        BackColor = Color.FromArgb(15, 20, 28)

        Call BuildLayout()
        Call BuildActions()
        Call BuildTuning()
        Call BuildBrainPanel()
        Call BuildStatus()

        Call orbit.LoadLevel(env.Level)
        Call SyncSliders()

        AddHandler clock.Tick, AddressOf OnTick
        clock.Interval = 15
        clock.Start()
        watch.Start()
    End Sub

    ' /********************************************************************************/
    '  布局
    ' /********************************************************************************/

    Private Sub BuildLayout()
        layout.Dock = DockStyle.Fill
        layout.BackColor = Color.FromArgb(15, 20, 28)
        layout.Orientation = Orientation.Vertical
        layout.SplitterWidth = 6

        orbit.Canvas.Dock = DockStyle.Fill
        orbit.Canvas.BackgroundColor = Color.FromArgb(15, 20, 28)
        layout.Panel1.Controls.Add(orbit.Canvas)

        rightColumn.Dock = DockStyle.Fill
        rightColumn.BackColor = Color.FromArgb(23, 30, 40)

        headCanvas.Dock = DockStyle.Top
        headCanvas.Height = 240
        headCanvas.AutoClear = False
        headCanvas.BackgroundColor = Color.FromArgb(15, 20, 28)
        headCanvas.VSync = False
        AddHandler headCanvas.Render, AddressOf OnHeadRender

        controlsPanel.Dock = DockStyle.Fill
        controlsPanel.AutoScroll = True
        controlsPanel.BackColor = Color.FromArgb(23, 30, 40)
        controlsPanel.Padding = New Padding(10, 6, 10, 6)

        ' 用纵向流式布局堆叠控件：普通 Panel 不会自动排列，
        ' 后加入的 AutoSize 控件会全部叠在左上角
        controlsPanel.FlowDirection = FlowDirection.TopDown
        controlsPanel.WrapContents = False

        statusBar.Dock = DockStyle.Bottom
        statusBar.Height = 116
        statusBar.BackColor = Color.FromArgb(15, 20, 28)
        statusBar.Padding = New Padding(10, 6, 10, 6)

        rightColumn.Controls.Add(controlsPanel)
        rightColumn.Controls.Add(statusBar)
        rightColumn.Controls.Add(headCanvas)

        layout.Panel2.Controls.Add(rightColumn)

        Controls.Add(layout)
    End Sub

    Private Function TitleLabel(text As String) As Label
        Return New Label With {
            .Text = text,
            .AutoSize = True,
            .ForeColor = Color.FromArgb(110, 127, 146),
            .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold),
            .Margin = New Padding(2, 8, 2, 2)
        }
    End Function

    Private Function MakeButton(text As String, handler As EventHandler) As Button
        Dim btn As New Button With {
            .Text = text,
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(33, 43, 56),
            .ForeColor = Color.FromArgb(232, 238, 246),
            .Font = New Font("Segoe UI", 9.0F),
            .Size = New Size(104, 30),
            .Margin = New Padding(3),
            .Cursor = Cursors.Hand,
            .UseVisualStyleBackColor = False
        }

        btn.FlatAppearance.BorderColor = Color.FromArgb(46, 123, 224)
        btn.FlatAppearance.BorderSize = 1
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 123, 224)
        AddHandler btn.Click, handler

        Return btn
    End Function

    Private Sub BuildActions()
        controlsPanel.Controls.Add(TitleLabel("预设动作"))

        Dim table As New TableLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .ColumnCount = 2,
            .RowCount = 5,
            .BackColor = Color.Transparent
        }

        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))

        table.Controls.Add(MakeButton("站立", Sub(s, e) Call SetAction(ActionPreset.Stand)), 0, 0)
        table.Controls.Add(MakeButton("行走", Sub(s, e) Call SetAction(ActionPreset.Walk)), 1, 0)
        table.Controls.Add(MakeButton("奔跑", Sub(s, e) Call SetAction(ActionPreset.Run)), 0, 1)
        table.Controls.Add(MakeButton("左转", Sub(s, e) Call SetAction(ActionPreset.TurnLeft)), 1, 1)
        table.Controls.Add(MakeButton("右转", Sub(s, e) Call SetAction(ActionPreset.TurnRight)), 0, 2)
        table.Controls.Add(MakeButton("跳跃", Sub(s, e) Call SetAction(ActionPreset.Jump)), 1, 2)
        table.Controls.Add(MakeButton("跨越障碍", Sub(s, e) Call SetAction(ActionPreset.StepOver)), 0, 3)
        table.Controls.Add(MakeButton("上台阶", Sub(s, e) Call SetAction(ActionPreset.ClimbStairs)), 1, 3)
        table.Controls.Add(MakeButton("停止", Sub(s, e) Call SetAction(ActionPreset.Halt)), 0, 4)
        table.Controls.Add(MakeButton("复位角色", Sub(s, e) Call ResetFigure()), 1, 4)

        controlsPanel.Controls.Add(table)

        controlsPanel.Controls.Add(TitleLabel("自动演示"))

        btnDemo = MakeButton("播放演示", AddressOf OnToggleDemo)
        btnDemo.Dock = DockStyle.Top
        controlsPanel.Controls.Add(btnDemo)

        controlsPanel.Controls.Add(TitleLabel("视角 / 显示"))

        Dim viewRow As New TableLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .ColumnCount = 2,
            .BackColor = Color.Transparent
        }

        viewRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        viewRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        viewRow.Controls.Add(MakeButton("重置视角", Sub(s, e) Call orbit.ResetCamera()), 0, 0)
        viewRow.Controls.Add(MakeButton("保存截图", Sub(s, e) Call SaveScreenshot()), 1, 0)

        controlsPanel.Controls.Add(viewRow)

        chkFollow = MakeCheck("相机跟随火柴人", orbit.FollowFigure, AddressOf OnDisplayChanged)
        chkJoints = MakeCheck("显示关节标记", orbit.ShowJoints, AddressOf OnDisplayChanged)
        chkGrid = MakeCheck("显示地面网格", orbit.ShowGrid, AddressOf OnDisplayChanged)
        chkSelfBody = MakeCheck("头部视角显示自身", headView.ShowSelfBody, AddressOf OnDisplayChanged)

        controlsPanel.Controls.Add(chkFollow)
        controlsPanel.Controls.Add(chkJoints)
        controlsPanel.Controls.Add(chkGrid)
        controlsPanel.Controls.Add(chkSelfBody)
    End Sub

    Private Function MakeCheck(text As String, checked As Boolean, handler As EventHandler) As CheckBox
        Dim chk As New CheckBox With {
            .Text = text,
            .Checked = checked,
            .AutoSize = True,
            .ForeColor = Color.FromArgb(169, 182, 198),
            .Font = New Font("Segoe UI", 9.0F),
            .Margin = New Padding(4, 3, 2, 3)
        }

        AddHandler chk.CheckedChanged, handler
        Return chk
    End Function

    Private Function MakeSlider(min As Integer, max As Integer, value As Integer, handler As EventHandler) As TrackBar
        Dim trk As New TrackBar With {
            .Minimum = min,
            .Maximum = max,
            .Value = value,
            .TickFrequency = 5,
            .SmallChange = 1,
            .LargeChange = 5,
            .Dock = DockStyle.Top,
            .AutoSize = False,
            .Height = 26
        }

        AddHandler trk.ValueChanged, handler
        Return trk
    End Function

    Private Sub BuildTuning()
        controlsPanel.Controls.Add(TitleLabel("参数调节"))

        controlsPanel.Controls.Add(New Label With {
            .Text = "行走速度",
            .AutoSize = True,
            .ForeColor = Color.FromArgb(169, 182, 198),
            .Font = New Font("Segoe UI", 8.5F),
            .Margin = New Padding(2, 4, 2, 0)
        })

        trkSpeed = MakeSlider(2, 30, 10, AddressOf OnSpeedChanged)
        controlsPanel.Controls.Add(trkSpeed)

        controlsPanel.Controls.Add(New Label With {
            .Text = "马达增益",
            .AutoSize = True,
            .ForeColor = Color.FromArgb(169, 182, 198),
            .Font = New Font("Segoe UI", 8.5F),
            .Margin = New Padding(2, 6, 2, 0)
        })

        trkStiffness = MakeSlider(4, 20, 10, AddressOf OnStiffnessChanged)
        controlsPanel.Controls.Add(trkStiffness)

        controlsPanel.Controls.Add(New Label With {
            .Text = "参数即时生效，无需重启仿真。",
            .AutoSize = True,
            .ForeColor = Color.FromArgb(110, 127, 146),
            .Font = New Font("Segoe UI", 8.0F),
            .Margin = New Padding(2, 8, 2, 2)
        })
    End Sub

    Private Function StatusLabel(text As String) As Label
        Return New Label With {
            .Text = text,
            .AutoSize = True,
            .ForeColor = Color.FromArgb(110, 127, 146),
            .Font = New Font("Consolas", 8.5F),
            .Margin = New Padding(2)
        }
    End Function

    Private Sub BuildStatus()
        Dim flow As New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .AutoScroll = False,
            .BackColor = Color.Transparent
        }

        lblTitle = StatusLabel("3D 火柴人物理仿真 Demo")
        lblTitle.ForeColor = Color.FromArgb(78, 168, 255)
        lblTitle.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)

        lblAction = StatusLabel("动作: 站立")
        lblTime = StatusLabel("仿真时间: 0.00 s")
        lblPose = StatusLabel("位置: --   高度: --   速度: --")
        lblContact = StatusLabel("足部触地: 左=-  右=-")
        lblFps = StatusLabel("FPS: --   物理耗时: -- ms")
        lblFallen = StatusLabel("状态: 正常")
        lblAgent = StatusLabel("Agent 接口: GetObservation / SetAction / Act / GetVisionGray")

        lblAgent.ForeColor = Color.FromArgb(124, 227, 139)

        flow.Controls.Add(lblTitle)
        flow.Controls.Add(lblAction)
        flow.Controls.Add(lblTime)
        flow.Controls.Add(lblPose)
        flow.Controls.Add(lblContact)
        flow.Controls.Add(lblFps)
        flow.Controls.Add(lblFallen)
        flow.Controls.Add(lblAgent)

        statusBar.Controls.Add(flow)
    End Sub

    ' /********************************************************************************/
    '  动作与参数
    ' /********************************************************************************/

    Private Sub SetAction(action As ActionPreset)
        Call env.Act(action)
    End Sub

    Private Sub ResetFigure()
        Call env.Reset()
    End Sub

    Private Sub OnToggleDemo(sender As Object, e As EventArgs)
        If env.Director.DemoRunning Then
            Call env.Director.StopDemo()
            btnDemo.Text = "播放演示"
            btnDemo.BackColor = Color.FromArgb(33, 43, 56)
        Else
            Call env.Director.StartDemo(ActionDirector.DefaultScript())
            btnDemo.Text = "停止演示"
            btnDemo.BackColor = Color.FromArgb(46, 123, 224)
        End If
    End Sub

    Private Sub SyncSliders()
        trkSpeed.Value = CInt(std.Min(trkSpeed.Maximum, std.Max(trkSpeed.Minimum, env.Gait.WalkSpeed * 10.0)))
        trkStiffness.Value = CInt(std.Min(trkStiffness.Maximum,
                                          std.Max(trkStiffness.Minimum, env.Skeleton.MotorGainScale * 10.0)))
    End Sub

    Private Sub OnSpeedChanged(sender As Object, e As EventArgs)
        env.Gait.WalkSpeed = trkSpeed.Value / 10.0
    End Sub

    Private Sub OnStiffnessChanged(sender As Object, e As EventArgs)
        Call env.Skeleton.ApplyMotorGain(trkStiffness.Value / 10.0)
    End Sub

    Private Sub OnDisplayChanged(sender As Object, e As EventArgs)
        orbit.FollowFigure = chkFollow.Checked
        orbit.ShowJoints = chkJoints.Checked
        orbit.ShowGrid = chkGrid.Checked
        headView.ShowSelfBody = chkSelfBody.Checked
    End Sub

    Private Sub SaveScreenshot()
        Using dialog As New SaveFileDialog With {
            .Filter = "PNG 图片 (*.png)|*.png",
            .FileName = $"stickfigure_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        }
            If dialog.ShowDialog(Me.FindForm()) = DialogResult.OK Then
                Call orbit.SaveSnapshot(dialog.FileName)
            End If
        End Using
    End Sub

    ' /********************************************************************************/
    '  主循环
    ' /********************************************************************************/

    Private Sub OnHeadRender(sender As Object, e As DxRenderEventArgs)
        ' 第一人称画面：与神经网络取帧用的是同一套绘制代码
        Call headView.Render(e.Graphics, env.Level.Mesh, env.FigureLines())
    End Sub

    Private Sub OnTick(sender As Object, e As EventArgs)
        Dim elapsed As Double = watch.Elapsed.TotalMilliseconds
        Dim frameMs As Double = elapsed - lastFrameMs

        lastFrameMs = elapsed

        If frameMs > 0 AndAlso frameMs < 1000.0 Then
            Dim instant As Double = 1000.0 / frameMs
            fps = If(fps <= 0, instant, fps * 0.9 + instant * 0.1)
        End If

        ' 步长夹在 [1/120, 1/20]，避免窗口失焦回来之后物理"补帧"爆炸
        Dim dt As Double = std.Min(1.0 / 20.0, std.Max(1.0 / 120.0, frameMs / 1000.0))

        Call env.Step(dt)

        ' 更新骨架线段 + 跟随相机（内部已调用 RequestRender）
        Call orbit.UpdateFigure(env.Skeleton, env.Position)

        Call headCanvas.Invalidate()

        Call UpdateStatus()
    End Sub

    Private Sub UpdateStatus()
        Dim p As Vec3 = env.Position
        Dim v As Vec3 = Vec3.FromPhysics(env.Skeleton.Bodies(BoneIndex.Pelvis).Velocity)
        Dim speed As Double = New Vec3(v.X, 0, v.Z).Length

        lblAction.Text = "动作: " & env.Director.StatusText
        lblTime.Text = $"仿真时间: {env.Time:F2} s   相位: {env.Gait.Phase:F2}"
        lblPose.Text = $"位置: {p.X:F2}, {p.Z:F2}   高度: {p.Y:F2} m   速度: {speed:F2} m/s"
        lblContact.Text = $"足部触地: 左={If(env.Skeleton.FootContact.Left, "●", "-")}  右={If(env.Skeleton.FootContact.Right, "●", "-")}"
        lblFps.Text = $"FPS: {fps:F0}   物理耗时: {env.LastStepMs:F1} ms"

        If env.IsFallen Then
            lblFallen.Text = "状态: 跌倒（即将自动复位）"
            lblFallen.ForeColor = Color.FromArgb(255, 107, 107)
        Else
            lblFallen.Text = "状态: 正常"
            lblFallen.ForeColor = Color.FromArgb(124, 227, 139)
        End If
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)

        If layout.Panel1 IsNot Nothing AndAlso Width > 200 Then
            Dim distance As Integer = CInt(Width * 0.68)

            If distance > layout.Panel1MinSize AndAlso
               distance < Width - layout.Panel2MinSize - layout.SplitterWidth Then
                layout.SplitterDistance = distance
            End If
        End If
    End Sub

    ''' <summary>暴露环境，便于外部脚本 / 调试器直接驱动。</summary>
    Public ReadOnly Property Environment As FigureEnvironment
        Get
            Return env
        End Get
    End Property

    ''' <summary>主视角封装。</summary>
    Public ReadOnly Property MainView As OrbitView
        Get
            Return orbit
        End Get
    End Property

    ''' <summary>头部视角封装（第一人称画面，神经网络输入源）。</summary>
    Public ReadOnly Property Head As HeadViewRenderer
        Get
            Return headView
        End Get
    End Property
End Class
