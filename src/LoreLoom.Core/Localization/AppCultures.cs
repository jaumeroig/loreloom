using System.Globalization;

namespace LoreLoom.Core.Localization;

public static class AppCultures
{
    public const string Catalan = "ca-ES";
    public const string Spanish = "es-ES";
    public const string English = "en-US";
    public const string DefaultCulture = English;

    public static readonly IReadOnlyList<string> SupportedCultures = [Catalan, Spanish, English];

    public static string Normalize(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return DefaultCulture;

        var normalized = culture.Trim();

        if (normalized.Equals(Catalan, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ca", StringComparison.OrdinalIgnoreCase))
        {
            return Catalan;
        }

        if (normalized.Equals(Spanish, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("es", StringComparison.OrdinalIgnoreCase))
        {
            return Spanish;
        }

        if (normalized.Equals(English, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            return English;
        }

        try
        {
            var cultureInfo = CultureInfo.GetCultureInfo(normalized);
            var language = cultureInfo.TwoLetterISOLanguageName;
            return Normalize(language);
        }
        catch (CultureNotFoundException)
        {
            return DefaultCulture;
        }
    }
}
