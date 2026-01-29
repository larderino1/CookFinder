namespace CookFinder.Bot.Repositories;

public interface IUserPreferenceRepository
{
    Task<string> GetLanguageAsync(long userId, CancellationToken cancellationToken);
    Task SetLanguageAsync(long userId, string language, CancellationToken cancellationToken);
}
