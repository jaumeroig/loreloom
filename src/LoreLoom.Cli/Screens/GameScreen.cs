using LoreLoom.Core.Dtos;
using LoreLoom.Core.Enums;
using Spectre.Console;
using Res = LoreLoom.Cli.Resources.Resources;

namespace LoreLoom.Cli.Screens;

public static class GameScreen
{
    public static async Task Run(ApiClient api, Guid gameId, string token)
    {
        while (true)
        {
            var game = await api.GetGame(gameId);
            if (game is null) return;

            if (game.Status == GameStatus.Finished)
            {
                await ShowGameOver(api, gameId);
                return;
            }

            AnsiConsole.Clear();
            RenderResourceBar(game.ResourceName, game.ResourcePct);
            AnsiConsole.WriteLine();

            // Show recent turns
            var turns = await api.GetTurns(gameId);
            var recentTurns = turns.TakeLast(5).ToList();
            foreach (var turn in recentTurns)
            {
                AnsiConsole.MarkupLine($"[bold cyan]{Markup.Escape(turn.PlayerName)}:[/] {Markup.Escape(turn.PlayerAction)}");
                AnsiConsole.Write(new Panel(Markup.Escape(turn.DmResponse))
                    .Header($"[bold yellow]{Markup.Escape(Res.DmSays)}[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Yellow));
                AnsiConsole.WriteLine();
            }

            // Check if it's our turn
            var myPlayer = game.Players.FirstOrDefault(p => p.Name == GetMyPlayerName(game, token));
            if (myPlayer is null)
            {
                // Find player by token (we need to match via the game)
                myPlayer = game.Players.FirstOrDefault(p => true); // fallback
            }

            var currentPlayer = game.Players.FirstOrDefault(p => p.IsCurrentTurn);
            var isMyTurn = currentPlayer is not null && IsMyPlayer(game, currentPlayer, token);

            if (!isMyTurn)
            {
                var currentName = currentPlayer?.Name ?? "???";
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(string.Format(Res.NotYourTurn, currentName))}[/]");
                await Task.Delay(3000);
                continue;
            }

            // My turn!
            AnsiConsole.MarkupLine($"[bold green]{Markup.Escape(Res.YourTurn)}[/]");
            AnsiConsole.WriteLine();

            var action = AnsiConsole.Prompt(
                new TextPrompt<string>($"[bold yellow]> {Markup.Escape(Res.EnterAction)}[/]:"));

            TurnResultResponse? result = null;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("yellow"))
                .StartAsync(Res.WaitingForDm, async _ =>
                {
                    result = await api.SendTurn(gameId, new SendTurnRequest(token, action));
                });

            if (result is null) continue;

            AnsiConsole.WriteLine();
            RenderResourceBar(game.ResourceName, result.ResourcePctAfter);
            AnsiConsole.WriteLine();

            AnsiConsole.Write(new Panel(Markup.Escape(result.Narrative))
                .Header($"[bold yellow]{Markup.Escape(Res.DmSays)}[/]")
                .Border(BoxBorder.Heavy)
                .BorderColor(Color.Yellow));

            var costColor = result.ResourceCost < 0 ? "green" : "red";
            var costSign = result.ResourceCost < 0 ? "" : "-";
            AnsiConsole.MarkupLine($"[{costColor}]{Markup.Escape(string.Format(Res.ResourceCost, $"{costSign}{result.ResourceCost}"))}[/]");

            if (result.GameOver)
            {
                AnsiConsole.WriteLine();
                await ShowGameOver(api, gameId, result);
                return;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(Res.PressEnter)}[/]");
            Console.ReadLine();
        }
    }

    private static void RenderResourceBar(string resourceName, int pct)
    {
        pct = Math.Max(0, Math.Min(100, pct));
        var color = pct switch
        {
            > 60 => Color.Green,
            > 30 => Color.Yellow,
            _ => Color.Red
        };

        var filled = pct / 5;
        var empty = 20 - filled;
        var bar = new string('\u2588', filled) + new string('\u2591', empty);

        var panel = new Panel($"[bold {color}]{Markup.Escape(string.Format(Res.ResourceBar, resourceName, pct))}  {bar}[/]")
            .Border(BoxBorder.Double)
            .BorderColor(color);

        AnsiConsole.Write(panel);
    }

    private static async Task ShowGameOver(ApiClient api, Guid gameId, TurnResultResponse? lastResult = null)
    {
        AnsiConsole.Clear();

        var result = await api.GetResult(gameId);
        var isVictory = result?.Victory ?? lastResult?.Victory ?? false;

        var header = isVictory ? Res.GameOverVictory : Res.GameOverDefeat;
        var headerColor = isVictory ? Color.Green : Color.Red;

        AnsiConsole.Write(new FigletText(header).Color(headerColor).Centered());
        AnsiConsole.WriteLine();

        // Postmortem
        var postmortem = lastResult?.Postmortem ?? result?.Postmortem;
        if (!string.IsNullOrWhiteSpace(postmortem))
        {
            AnsiConsole.Write(new Panel(Markup.Escape(postmortem))
                .Header($"[bold]{Markup.Escape(Res.Postmortem)}[/]")
                .Border(BoxBorder.Double)
                .BorderColor(Color.Purple));
            AnsiConsole.WriteLine();
        }

        // Results table
        if (result?.Players is { Count: > 0 })
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(Res.Player)
                .AddColumn(Res.XpEarned)
                .AddColumn(Res.Points);

            foreach (var p in result.Players)
                table.AddRow(Markup.Escape(p.Name), p.XpEarned.ToString(), p.PointsEarned.ToString());

            AnsiConsole.Write(new Panel(table).Header($"[bold]{Markup.Escape(Res.Results)}[/]"));
        }

        AnsiConsole.WriteLine();

        // Export
        if (AnsiConsole.Confirm($"[yellow]{Markup.Escape(Res.ExportQuestion)}[/]", false))
        {
            var md = await api.ExportGame(gameId);
            if (md is not null)
            {
                var fileName = $"loreloom-{gameId.ToString()[..8]}.md";
                await File.WriteAllTextAsync(fileName, md);
                AnsiConsole.MarkupLine($"[green]{Markup.Escape(string.Format(Res.ExportSaved, fileName))}[/]");
            }
        }

        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(Res.PressEnter)}[/]");
        Console.ReadLine();
    }

    private static string? GetMyPlayerName(GameResponse game, string token)
    {
        // We can't see tokens in the response, so we'll rely on position
        return null;
    }

    private static bool IsMyPlayer(GameResponse game, PlayerResponse player, string token)
    {
        // The API doesn't expose tokens in the response for security.
        // We'll match by checking if sending a turn succeeds.
        // For now, always assume it's our turn if IsCurrentTurn is true
        // and we'll let the API validate the token.
        return player.IsCurrentTurn;
    }
}
