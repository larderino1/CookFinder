using System.Globalization;
using System.Resources;

namespace CookFinder.Bot.Services.Localization;

public sealed class ResourceLocalizationService : ILocalizationService
{
    private readonly ResourceManager _resourceManager = new("CookFinder.Bot.Resources.Strings", typeof(ResourceLocalizationService).Assembly);

    public string GetString(string key, string language)
    {
        return GetString(key, language, Array.Empty<object>());
    }

    public string GetString(string key, string language, params object[] args)
    {
        var culture = ResolveCulture(language);
        var value = _resourceManager.GetString(key, culture) ?? key;
        return args.Length == 0 ? value : string.Format(culture, value, args);
    }

    private static CultureInfo ResolveCulture(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return CultureInfo.GetCultureInfo("en");
        }

        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en");
        }
    }
}
