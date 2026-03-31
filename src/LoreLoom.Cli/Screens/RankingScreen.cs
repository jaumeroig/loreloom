using Spectre.Console;
using Res = LoreLoom.Cli.Resources.Resources;

namespace LoreLoom.Cli.Screens;

public static class RankingScreen
{
    public static async Task Run(ApiClient api)
    {
        AnsiConsole.Clear();

        var ranking = await api.GetRanking();

        if (ranking.Count == 0)
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(Res.RankingEmpty)}[/]");
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(Res.Rank)
                .AddColumn(Res.Player)
                .AddColumn(Res.CharacterName)
                .AddColumn(Res.Points)
                .AddColumn(Res.GamesPlayed);

            foreach (var entry in ranking)
            {
                var rankDisplay = entry.Rank switch
                {
                    1 => "[gold1]1[/]",
                    2 => "[silver]2[/]",
                    3 => "[orange3]3[/]",
                    _ => entry.Rank.ToString()
                };

                table.AddRow(
                    new Markup(rankDisplay),
                    new Markup(Markup.Escape(entry.PlayerName)),
                    new Markup(Markup.Escape(entry.CharacterName ?? "—")),
                    new Markup(entry.TotalPoints.ToString()),
                    new Markup(entry.GamesPlayed.ToString()));
            }

            AnsiConsole.Write(new Panel(table)
                .Header($"[bold purple]{Markup.Escape(Res.RankingTitle)}[/]")
                .Border(BoxBorder.Double)
                .BorderColor(Color.Purple));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(Res.PressEnter)}[/]");
        Console.ReadLine();
    }
}
