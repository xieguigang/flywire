Imports System.Drawing
Imports System.Linq
Imports Snake2


    ''' <summary>
    ''' 从游戏的真实状态编码感觉通道，并给出一个"示范动作"（用于训练读出层）。
    ''' </summary>
    ''' <remarks>
    ''' 本类只读游戏状态，不改动游戏：它把"蛇看到的世界"翻译成 16 个通道的强度。
    ''' 运动的接管在 <see cref="SnakeSession"/> 里完成。
    ''' </remarks>
    Public NotInheritable Class SnakeSensorEncoder

        ''' <summary>动作编号 → 绝对方向。</summary>
        Public Const ActionCount As Integer = 4

        ''' <summary>编码当前游戏状态。</summary>
        Public Shared Function Encode(game As Game) As SnakeSensorFrame
            Dim snake As Snake = game.playerSnake
            Dim values As Double() = New Double(SnakeSensors.ChannelCount - 1) {}
            Dim heading As Point = snake.Direction

            If heading = Point.Empty Then heading = New Point(1, 0)

            ' ---- 0..7 常规食物方位（以朝向为参考系）----
            Dim food As Food = nearestFood(game, snake.Head, RegularFoodKinds)
            Dim foodDistance As Integer = -1

            If food IsNot Nothing Then
                Dim delta As New Point(food.Position.X - snake.Head.X, food.Position.Y - snake.Head.Y)
                Dim distance As Double = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y)

                foodDistance = CInt(distance)
                values(relativeSector(heading, delta, SnakeSensors.FoodSectors)) =
                    Math.Max(0.0, 1.0 - distance / SnakeSensors.SenseRadius)
            End If

            ' ---- 8..11 碰撞危险（绝对方向）----
            Dim blocked As Integer = 0

            For i As Integer = 0 To SnakeSensors.DangerChannels - 1
                Dim cell As Point = New Point(snake.Head.X + SnakeSensors.Directions(i).X,
                                              snake.Head.Y + SnakeSensors.Directions(i).Y)

                If IsBlocked(game, cell, snake) Then
                    values(SnakeSensors.FoodSectors + i) = 1.0
                    blocked += 1
                End If
            Next

            ' ---- 12..15 特殊食物方位（活动食物 / 超级食物，粗扇区）----
            Dim special As Food = nearestFood(game, snake.Head, SpecialFoodKinds)

            If special IsNot Nothing Then
                Dim delta As New Point(special.Position.X - snake.Head.X, special.Position.Y - snake.Head.Y)
                Dim distance As Double = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y)
                Dim channel As Integer = SnakeSensors.FoodSectors + SnakeSensors.DangerChannels +
                                         relativeSector(heading, delta, SnakeSensors.SpecialFoodSectors)

                values(channel) = Math.Max(values(channel), Math.Max(0.0, 1.0 - distance / SnakeSensors.SenseRadius))

                ' 面板上的"距离食物"显示的是最近的那份猎物，它可能正是这个特殊食物
                If foodDistance < 0 OrElse distance < foodDistance Then foodDistance = CInt(distance)
            End If

            Return New SnakeSensorFrame With {
                .Values = values,
                .BlockedCells = blocked,
                .FoodDistance = foodDistance
            }
        End Function

        ''' <summary>把绝对位移折算成"以朝向为参考系"的扇区编号。</summary>
        Private Shared Function relativeSector(heading As Point, delta As Point, sectors As Integer) As Integer
            Dim right As New Point(heading.Y, -heading.X)      ' 右手方向
            Dim forwardComponent As Double = delta.X * heading.X + delta.Y * heading.Y
            Dim rightComponent As Double = delta.X * right.X + delta.Y * right.Y
            Dim angle As Double = Math.Atan2(rightComponent, forwardComponent)
            Dim unit As Double = 2.0 * Math.PI / sectors
            Dim sector As Integer = CInt(Math.Round(angle / unit))

            sector = sector Mod sectors

            If sector < 0 Then sector += sectors

            Return sector
        End Function

        ''' <summary>某格是否不可进入（越界 / 障碍物 / 蛇身）。</summary>
        Public Shared Function IsBlocked(game As Game, cell As Point, snake As Snake) As Boolean
            If cell.X < 0 OrElse cell.Y < 0 OrElse cell.X >= Game.MapCols OrElse cell.Y >= Game.MapRows Then
                Return True
            End If

            If game.obstacles.Contains(cell) Then Return True

            ' 自己的身体：跳过蛇头自己（头已经离开那一格了），尾部会随时间让开也一并保守处理
            If snake.ContainsBody(cell, skipHead:=True) Then Return True

            For Each other As Snake In game.aiSnakes
                If other.Alive AndAlso other.ContainsBody(cell) Then Return True
            Next

            Return False
        End Function

        ''' <summary>
        ''' 示范动作：在各条可行方向里挑"最靠近最优猎物"的那一条。
        ''' </summary>
        ''' <remarks>
        ''' 这只是一个供训练用的<b>教师</b>（贪心策略），不是游戏的操作逻辑本身 ——
        ''' 果蝇大脑的运动由读出层解码得到，教师只提供训练样本。
        ''' 若所有方向都被堵住，则保持原方向（反正怎么走都要撞）。
        ''' 
        ''' <b>猎物不限于常规食物</b>：常规食物 1 分，活动食物 5 分，超级食物 30 分。
        ''' 全图上常驻几百只活动食物，而常规食物只有 3 只 —— 教师如果只盯着常规食物，
        ''' 就会带着蛇横穿整张地图去追一份 1 分的食物（实测最近的那份常规食物在 142 格开外），
        ''' 期间对身边一堆 5 分的猎物视而不见。因此这里按"价值折算成距离折扣"来选目标：
        ''' 代价 = 曼哈顿距离 − 价值，代价最小的猎物就是目标，再朝它走一步。
        ''' </remarks>
        Public Shared Function TeacherAction(game As Game) As Integer
            Dim snake As Snake = game.playerSnake
            Dim heading As Point = snake.Direction
            Dim reverse As New Point(-heading.X, -heading.Y)
            Dim best As Integer = actionOf(heading, snake.Direction)
            Dim bestScore As Double = Double.MaxValue

            For action As Integer = 0 To ActionCount - 1
                Dim direction As Point = SnakeSensors.Directions(action)

                ' 蛇不能反向
                If direction = reverse Then Continue For

                Dim cell As New Point(snake.Head.X + direction.X, snake.Head.Y + direction.Y)

                If IsBlocked(game, cell, snake) Then Continue For

                Dim score As Double = preyCost(game, cell)

                ' 同分时优先保持原方向，避免原地抖动
                If direction = heading Then score -= 0.25

                If score < bestScore Then
                    bestScore = score
                    best = action
                End If
            Next

            Return best
        End Function

        ''' <summary>走到 <paramref name="cell"/> 之后，离"最划算的猎物"还有多远（越小越好）。</summary>
        ''' <remarks>
        ''' 对每份食物各算一次"折算代价" = 曼哈顿距离 − 价值折分（越贵的越值得多走几步），
        ''' 取其中最小的那个。全图没有食物时返回 0（四处找吃的都一样）。
        ''' </remarks>
        Private Shared Function preyCost(game As Game, cell As Point) As Double
            Dim best As Double = Double.MaxValue

            For Each food As Food In game.foods
                Dim distance As Double = Math.Abs(cell.X - food.Position.X) + Math.Abs(cell.Y - food.Position.Y)
                Dim cost As Double = distance - valueOf(food.Type)

                If cost < best Then best = cost
            Next

            If best = Double.MaxValue Then Return 0.0

            Return best
        End Function

        ''' <summary>猎物分值（与游戏内的计分一致：常规 1 / 活动 5 / 超级 30）。</summary>
        Private Shared Function valueOf(type As FoodType) As Double
            Select Case type
                Case FoodType.Moving
                    Return 5.0
                Case FoodType.Super
                    Return 30.0
                Case Else
                    Return 1.0
            End Select
        End Function

        ''' <summary>方向 → 动作编号（找不到时返回 <paramref name="fallback"/>，再找不到就返回"向右"）。</summary>
        ''' <remarks>
        ''' 备用参数用 <c>Point?</c>：<see cref="Point"/> 是值类型，
        ''' 值类型不能与 <c>Nothing</c> 比较（"IsNot 操作数必须是引用类型"）。
        ''' </remarks>
        Public Shared Function actionOf(direction As Point, Optional fallback As Point? = Nothing) As Integer
            For i As Integer = 0 To ActionCount - 1
                If SnakeSensors.Directions(i) = direction Then Return i
            Next

            If fallback.HasValue Then
                For i As Integer = 0 To ActionCount - 1
                    If SnakeSensors.Directions(i) = fallback.Value Then Return i
                Next
            End If

            Return 3
        End Function

        ''' <summary>通道 0..7 关心的食物类型（常规食物）。</summary>
        Private Shared ReadOnly RegularFoodKinds As FoodType() = {FoodType.Regular}

        ''' <summary>通道 12..15 关心的食物类型（活动 / 超级食物）。</summary>
        Private Shared ReadOnly SpecialFoodKinds As FoodType() = {FoodType.Moving, FoodType.Super}

        ''' <summary>在指定的几类食物里找最近的一个（欧氏距离），没有则返回 Nothing。</summary>
        Private Shared Function nearestFood(game As Game, position As Point, kinds As FoodType()) As Food
            Dim best As Food = Nothing
            Dim bestDistance As Double = Double.MaxValue

            For Each food As Food In game.foods
                Dim matched As Boolean = False

                For Each kind As FoodType In kinds
                    If food.Type = kind Then
                        matched = True

                        Exit For
                    End If
                Next

                If Not matched Then Continue For

                Dim dx As Double = food.Position.X - position.X
                Dim dy As Double = food.Position.Y - position.Y
                Dim distance As Double = dx * dx + dy * dy

                If distance < bestDistance Then
                    bestDistance = distance
                    best = food
                End If
            Next

            Return best
        End Function

    End Class

