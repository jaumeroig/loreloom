using LoreLoom.Core.Dtos;
using Spectre.Console;
using Res = LoreLoom.Cli.Resources.Resources;

namespace LoreLoom.Cli.Screens;

public static class CreateGameScreen
{
    public static async Task<Guid?> Run(ApiClient api, string token)
    {
        AnsiConsole.Clear();

        var title = AnsiConsole.Prompt(new TextPrompt<string>($"[yellow]{Markup.Escape(Res.GameTitle)}[/]:"));
        var setting = AnsiConsole.Prompt(new TextPrompt<string>($"[yellow]{Markup.Escape(Res.GameSetting)}[/]:"));
        var resourceName = AnsiConsole.Prompt(new TextPrompt<string>($"[yellow]{Markup.Escape(Res.GameResourceName)}[/]:"));
        var maxPlayers = AnsiConsole.Prompt(new TextPrompt<int>($"[yellow]{Markup.Escape(Res.GameMaxPlayers)}[/]:").DefaultValue(4).Validate(v => v is >= 1 and <= 10));
        var isPublic = AnsiConsole.Confirm($"[yellow]{Markup.Escape(Res.GameIsPublic)}[/]", true);
        var language = AnsiConsole.Prompt(new TextPrompt<string>($"[yellow]{Markup.Escape(Res.GameLanguage)}[/]:").DefaultValue("English"));

        var playerName = AnsiConsole.Prompt(new TextPrompt<string>($"[yellow]{Markup.Escape(Res.PlayerName)}[/]:"));

        // Select character
        var characters = await api.GetCharacters(token);
        Guid? charId = null;
        if (characters.Count > 0)
        {
            var charChoices = characters.Select(c => c.Name).ToList();
            charChoices.Add(Res.NoCharOption);

            var charChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[yellow]{Markup.Escape(Res.SelectCharacter)}[/]")
                    .AddChoices(charChoices));

            if (charChoice != Res.NoCharOption)
                charId = characters.First(c => c.Name == charChoice).Id;
        }

        var request = new CreateGameRequest(title, setting, resourceName, token, playerName, charId, isPublic, maxPlayers, language);
        var game = await api.CreateGame(request);

        if (game is null) return null;

        AnsiConsole.MarkupLine($"[green]{Markup.Escape(string.Format(Res.GameCreated, game.Id))}[/]");
        if (game.InviteCode is not null)
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(string.Format(Res.GameInviteCode, game.InviteCode))}[/]");

        // Wait for players or start
        return await LobbyLoop(api, game.Id, token, language);
    }

    private static async Task<Guid?> LobbyLoop(ApiClient api, Guid gameId, string token, string language)
    {
        while (true)
        {
            var game = await api.GetGame(gameId);
            if (game is null) return null;

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(string.Format(Res.PlayersInGame, game.PlayerCount, game.MaxPlayers))}[/]");

            foreach (var p in game.Players)
                AnsiConsole.MarkupLine($"  [cyan]- {Markup.Escape(p.Name)}[/]");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .AddChoices(Res.StartGame, Res.WaitForPlayers));

            if (choice == Res.StartGame)
            {
                await api.StartGame(gameId, new StartGameRequest(token, language));
                AnsiConsole.MarkupLine($"[bold green]{Markup.Escape(Res.GameStarted)}[/]");
                return gameId;
            }

            // Wait a bit before refreshing
            await Task.Delay(2000);
        }
    }
}
