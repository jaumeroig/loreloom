using Spectre.Console;
using Res = LoreLoom.Cli.Resources.Resources;

namespace LoreLoom.Cli.Screens;

public enum MenuChoice
{
    Characters,
    JoinGame,
    CreateGame,
    Ranking,
    Language,
    Logout,
    Exit
}

public static class MainMenuScreen
{
    public static MenuChoice Run()
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold purple]{Markup.Escape(Res.MenuTitle)}[/]")
                .AddChoices(
                    Res.MenuCharacters,
                    Res.MenuJoinGame,
                    Res.MenuCreateGame,
                    Res.MenuRanking,
                    Res.MenuLanguage,
                    Res.MenuLogout,
                    Res.MenuExit));

        if (choice == Res.MenuCharacters) return MenuChoice.Characters;
        if (choice == Res.MenuJoinGame) return MenuChoice.JoinGame;
        if (choice == Res.MenuCreateGame) return MenuChoice.CreateGame;
        if (choice == Res.MenuRanking) return MenuChoice.Ranking;
        if (choice == Res.MenuLanguage) return MenuChoice.Language;
        if (choice == Res.MenuLogout) return MenuChoice.Logout;
        return MenuChoice.Exit;
    }
}
