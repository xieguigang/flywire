Imports Neuropils.RibbonLib.Controls
Imports RibbonLib

Module RibbonMenu

    Public ReadOnly Property ribbon As RibbonItems

    Public Sub Load(ribbon As Ribbon)
        _ribbon = New RibbonItems(ribbon)

        AddHandler RibbonMenu.ribbon.ButtonOpenSnakeGame.ExecuteEvent, Sub() Call Workbench.flywire.m_snake.openSnakeWindow()
    End Sub


End Module
