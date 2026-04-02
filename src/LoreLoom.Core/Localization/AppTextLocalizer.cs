using System.Globalization;

namespace LoreLoom.Core.Localization;

public sealed class AppTextLocalizer : IAppTextLocalizer
{
    public string this[string key] => Get(key);

    public string CurrentCulture => AppCultures.Normalize(CultureInfo.CurrentUICulture.Name);

    public string Get(string key, string? culture = null)
        => AppTextCatalog.Get(culture ?? CurrentCulture, key);

    public string Format(string key, params object[] arguments)
        => FormatForCulture(CurrentCulture, key, arguments);

    public string FormatForCulture(string culture, string key, params object[] arguments)
    {
        var template = Get(key, culture);
        return string.Format(CultureInfo.GetCultureInfo(AppCultures.Normalize(culture)), template, arguments);
    }
}
