using System.Globalization;
using LoreLoom.Core.Dtos;
using Spectre.Console;
using Res = LoreLoom.Cli.Resources.Resources;

namespace LoreLoom.Cli.Screens;

public static class AuthScreen
{
    public static async Task<(string Token, string Username)?> Run(ApiClient api, AppConfig config)
    {
        AnsiConsole.Write(new FigletText(Res.AppTitle).Color(Color.Purple));
        AnsiConsole.MarkupLine($"[italic grey]{Markup.Escape(Res.AppSubtitle)}[/]");
        AnsiConsole.WriteLine();

        // If we have a saved session, try to reuse it
        if (!string.IsNullOrWhiteSpace(config.PlayerToken) && !string.IsNullOrWhiteSpace(config.Username))
        {
            AnsiConsole.MarkupLine($"[green]{Markup.Escape(string.Format(Res.WelcomeBack, config.Username))}[/]");
            AnsiConsole.WriteLine();
            return (config.PlayerToken, config.Username);
        }

        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[yellow]{Markup.Escape(Res.AuthRegisterOrLogin)}[/]")
                    .AddChoices(Res.AuthLogin, Res.AuthRegister, Res.MenuLanguage, Res.MenuExit));

            if (choice == Res.MenuExit)
                return null;

            if (choice == Res.MenuLanguage)
            {
                ChangeLanguage(config);
                AnsiConsole.Clear();
                AnsiConsole.Write(new FigletText(Res.AppTitle).Color(Color.Purple));
                AnsiConsole.MarkupLine($"[italic grey]{Markup.Escape(Res.AppSubtitle)}[/]");
                AnsiConsole.WriteLine();
                continue;
            }

            var username = AnsiConsole.Prompt(
                new TextPrompt<string>($"[yellow]{Markup.Escape(Res.AuthUsername)}[/]:"));

            var password = AnsiConsole.Prompt(
                new TextPrompt<string>($"[yellow]{Markup.Escape(Res.AuthPassword)}[/]:")
                    .Secret());

            try
            {
                AuthResponse? result;

                if (choice == Res.AuthRegister)
                {
                    result = await api.Register(new RegisterRequest(username, password));
                    if (result is not null)
                        AnsiConsole.MarkupLine($"[green]{Markup.Escape(string.Format(Res.AuthRegisterSuccess, result.Username))}[/]");
                }
                else
                {
                    result = await api.Login(new LoginRequest(username, password));
                    if (result is not null)
                        AnsiConsole.MarkupLine($"[green]{Markup.Escape(string.Format(Res.AuthLoginSuccess, result.Username))}[/]");
                }

                if (result is not null)
                {
                    config.PlayerToken = result.Token;
                    config.Username = result.Username;
                    config.Save();

                    AnsiConsole.WriteLine();
                    return (result.Token, result.Username);
                }
            }
            catch (HttpRequestException ex)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(string.Format(Res.AuthError, ex.Message))}[/]");
                AnsiConsole.WriteLine();
            }
        }
    }

    private static void ChangeLanguage(AppConfig config)
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
        var culture = new CultureInfo(code);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        config.Language = code;
        config.Save();

        AnsiConsole.MarkupLine($"[green]{Markup.Escape(string.Format(Res.LanguageChanged, choice))}[/]");
        AnsiConsole.WriteLine();
    }
}
