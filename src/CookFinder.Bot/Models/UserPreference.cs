namespace CookFinder.Bot.Models;

public sealed class UserPreference
{
    public long UserId { get; init; }
    public string Language { get; init; } = "en";
}
