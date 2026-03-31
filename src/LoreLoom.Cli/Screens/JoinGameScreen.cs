using LoreLoom.Core.Dtos;
using LoreLoom.Core.Enums;
using Spectre.Console;
using Res = LoreLoom.Cli.Resources.Resources;

namespace LoreLoom.Cli.Screens;

public static class JoinGameScreen
{
    public static async Task<Guid?> Run(ApiClient api, string token)
    {
        AnsiConsole.Clear();

        // Show public games
        var games = await api.ListGames();

        if (games.Count > 0)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("ID")
                .AddColumn(Res.GameTitle)
                .AddColumn(Res.GameSetting)
                .AddColumn(Res.Player);

            foreach (var g in games)
                table.AddRow(
                    g.Id.ToString()[..8] + "...",
                    Markup.Escape(g.Title),
                    Markup.Escape(g.Setting),
                    $"{g.PlayerCount}/{g.MaxPlayers}");

            AnsiConsole.Write(new Panel(table).Header(Res.PublicGames));
        }
        else
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(Res.NoPublicGames)}[/]");
        }

        AnsiConsole.WriteLine();
        var input = AnsiConsole.Prompt(
            new TextPrompt<string>($"[yellow]{Markup.Escape(Res.EnterGameId)}[/]:")
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input)) return null;

        // Try to parse as GUID or find by invite code
        Guid gameId;
        string? inviteCode = null;

        if (Guid.TryParse(input, out var parsed))
        {
            gameId = parsed;
        }
        else
        {
            // Assume it's an invite code — search games
            inviteCode = input.Trim().ToUpperInvariant();
            // Try all public games first, otherwise we need the full ID
            var allGames = await api.ListGames();
            var match = allGames.FirstOrDefault(g => g.InviteCode == inviteCode);
            if (match is null)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(string.Format(Res.ErrorGeneric, "Game not found"))}[/]");
                return null;
            }
            gameId = match.Id;
        }

        var playerName = AnsiConsole.Prompt(
            new TextPrompt<string>($"[yellow]{Markup.Escape(Res.PlayerName)}[/]:"));

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

        var request = new JoinGameRequest(token, playerName, charId, inviteCode);
        var game = await api.JoinGame(gameId, request);

        if (game is null) return null;

        AnsiConsole.MarkupLine($"[green]{Markup.Escape(Res.JoinedGame)}[/]");

        // Wait for game to start
        while (true)
        {
            await Task.Delay(3000);
            var updated = await api.GetGame(gameId);
            if (updated is null) return null;
            if (updated.Status == GameStatus.Active)
            {
                AnsiConsole.MarkupLine($"[bold green]{Markup.Escape(Res.GameStarted)}[/]");
                return gameId;
            }
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(Res.WaitingForStart)}[/]");
        }
    }
}
