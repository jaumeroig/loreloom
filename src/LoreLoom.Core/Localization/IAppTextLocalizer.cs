namespace LoreLoom.Core.Localization;

public interface IAppTextLocalizer
{
    string this[string key] { get; }
    string CurrentCulture { get; }
    string Get(string key, string? culture = null);
    string Format(string key, params object[] arguments);
    string FormatForCulture(string culture, string key, params object[] arguments);
}
