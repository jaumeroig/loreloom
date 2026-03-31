using LoreLoom.Core.Dtos;
using Spectre.Console;
using Res = LoreLoom.Cli.Resources.Resources;

namespace LoreLoom.Cli.Screens;

public static class CharacterScreen
{
    public static async Task Run(ApiClient api, string token)
    {
        while (true)
        {
            AnsiConsole.Clear();
            var characters = await api.GetCharacters(token);

            if (characters.Count == 0)
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(Res.NoCharacters)}[/]");
            else
                ShowCharacterTable(characters);

            AnsiConsole.WriteLine();

            var choices = new List<string> { Res.CreateNewCharacter, Res.GoBack };
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold]{Markup.Escape(Res.CharactersTitle)}[/]")
                    .AddChoices(choices));

            if (choice == Res.GoBack) return;

            await CreateCharacter(api, token);
        }
    }

    private static void ShowCharacterTable(List<CharacterResponse> characters)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(Res.CharacterName)
            .AddColumn(Res.Level)
            .AddColumn(Res.XP)
            .AddColumn(Res.StatStrength)
            .AddColumn(Res.StatWit)
            .AddColumn(Res.StatCharisma);

        foreach (var c in characters)
        {
            table.AddRow(
                Markup.Escape(c.Name),
                c.Level.ToString(),
                $"{c.Xp}/{c.XpToNextLevel + c.Xp}",
                c.Strength.ToString(),
                c.Wit.ToString(),
                c.Charisma.ToString());
        }

        AnsiConsole.Write(table);
    }

    private static async Task CreateCharacter(ApiClient api, string token)
    {
        AnsiConsole.WriteLine();
        var name = AnsiConsole.Prompt(
            new TextPrompt<string>($"[yellow]{Markup.Escape(Res.CharacterName)}[/]:"));

        var backstory = AnsiConsole.Prompt(
            new TextPrompt<string>($"[yellow]{Markup.Escape(Res.CharacterBackstory)}[/]:")
                .AllowEmpty());

        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(Res.StatsInstruction)}[/]");

        int str, wit, cha;
        while (true)
        {
            str = AnsiConsole.Prompt(new TextPrompt<int>($"[yellow]{Markup.Escape(Res.StatStrength)}[/] (1-5):").Validate(v => v is >= 1 and <= 5));
            wit = AnsiConsole.Prompt(new TextPrompt<int>($"[yellow]{Markup.Escape(Res.StatWit)}[/] (1-5):").Validate(v => v is >= 1 and <= 5));
            cha = AnsiConsole.Prompt(new TextPrompt<int>($"[yellow]{Markup.Escape(Res.StatCharisma)}[/] (1-5):").Validate(v => v is >= 1 and <= 5));

            if (str + wit + cha == 9) break;

            AnsiConsole.MarkupLine($"[red]{Markup.Escape(string.Format(Res.StatsError, str + wit + cha))}[/]");
        }

        var request = new CreateCharacterRequest(token, name, string.IsNullOrWhiteSpace(backstory) ? null : backstory, str, wit, cha);
        var result = await api.CreateCharacter(request);

        if (result is not null)
            AnsiConsole.MarkupLine($"[green]{Markup.Escape(string.Format(Res.CharacterCreated, result.Name))}[/]");

        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(Res.PressEnter)}[/]");
        Console.ReadLine();
    }
}
