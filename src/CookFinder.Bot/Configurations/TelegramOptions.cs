namespace CookFinder.Bot.Configurations;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string Token { get; init; } = string.Empty;
}
