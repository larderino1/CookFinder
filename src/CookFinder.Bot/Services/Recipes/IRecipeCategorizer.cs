namespace CookFinder.Bot.Services.Recipes;

public interface IRecipeCategorizer
{
    Task<IReadOnlyList<string>> CategorizeAsync(string title, string description, CancellationToken cancellationToken);
}
