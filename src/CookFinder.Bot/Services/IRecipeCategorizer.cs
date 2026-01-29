namespace CookFinder.Bot.Services;

public interface IRecipeCategorizer
{
    Task<IReadOnlyList<string>> CategorizeAsync(string title, string description, CancellationToken cancellationToken);
}
