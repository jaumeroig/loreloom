using System.Globalization;
using LoreLoom.Cli;
using LoreLoom.Cli.Screens;
using Spectre.Console;
using Res = LoreLoom.Cli.Resources.Resources;

var config = AppConfig.Load();

// Restore saved language preference
if (!string.IsNullOrWhiteSpace(config.Language))
    SetCulture(config.Language);

var baseUrl = Environment.GetEnvironmentVariable("LORELOOM_API_URL") ?? "http://localhost:5000";
var api = new ApiClient(baseUrl);

try
{
    while (true)
    {
        var authResult = await AuthScreen.Run(api, config);
        if (authResult is null) return;

        var (token, _) = authResult.Value;
        var logout = false;
        while (!logout)
        {
            var choice = MainMenuScreen.Run();

            switch (choice)
            {
                case MenuChoice.Characters:
                    await CharacterScreen.Run(api, token);
                    break;

                case MenuChoice.CreateGame:
                    var createdGameId = await CreateGameScreen.Run(api, token);
                    if (createdGameId.HasValue)
                        await GameScreen.Run(api, createdGameId.Value, token);
                    break;

                case MenuChoice.JoinGame:
                    var joinedGameId = await JoinGameScreen.Run(api, token);
                    if (joinedGameId.HasValue)
                        await GameScreen.Run(api, joinedGameId.Value, token);
                    break;

                case MenuChoice.Ranking:
                    await RankingScreen.Run(api);
                    break;

                case MenuChoice.Language:
                    ChangeLanguage(config);
                    break;

                case MenuChoice.Logout:
                    config.PlayerToken = null;
                    config.Username = null;
                    config.Save();
                    AnsiConsole.Clear();
                    logout = true;
                    break;

                case MenuChoice.Exit:
                    return;
            }
        }
    }
}
catch (HttpRequestException)
{
    AnsiConsole.MarkupLine($"[red]{Markup.Escape(Res.ErrorConnection)}[/]");
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]{Markup.Escape(string.Format(Res.ErrorGeneric, ex.Message))}[/]");
}

static void ChangeLanguage(AppConfig config)
{
    var languages = new Dictionary<string, string>
    {
        ["English"] = "en",
        ["Català"] = "ca",
        ["Español"] = "es",
        ["Français"] = "fr",
        ["Deutsch"] = "de",
        ["Italiano"] = "it",
        ["Português"] = "pt",
        ["日本語"] = "ja"
    };

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title($"[yellow]{Markup.Escape(Res.SelectLanguage)}[/]")
            .AddChoices(languages.Keys));

    var code = languages[choice];
    SetCulture(code);

    config.Language = code;
    config.Save();

    AnsiConsole.MarkupLine($"[green]{Markup.Escape(string.Format(Res.LanguageChanged, choice))}[/]");
    AnsiConsole.WriteLine();
}

static void SetCulture(string code)
{
    var culture = new CultureInfo(code);
    CultureInfo.CurrentCulture = culture;
    CultureInfo.CurrentUICulture = culture;
}
