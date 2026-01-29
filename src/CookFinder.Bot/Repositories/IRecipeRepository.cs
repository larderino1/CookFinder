using CookFinder.Bot.Models;

namespace CookFinder.Bot.Repositories;

public interface IRecipeRepository
{
    Task SaveAsync(Recipe recipe, CancellationToken cancellationToken);
    Task<IReadOnlyList<Recipe>> GetByCategoryAsync(long userId, string category, CancellationToken cancellationToken);
    Task<IReadOnlyList<Recipe>> SearchByNameAsync(long userId, string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetCategoriesAsync(long userId, CancellationToken cancellationToken);
}
