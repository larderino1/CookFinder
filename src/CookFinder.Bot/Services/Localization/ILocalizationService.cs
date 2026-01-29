namespace CookFinder.Bot.Services.Localization;

public interface ILocalizationService
{
    string GetString(string key, string language);
    string GetString(string key, string language, params object[] args);
}
