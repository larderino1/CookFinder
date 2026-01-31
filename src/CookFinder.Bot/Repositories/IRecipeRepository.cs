using CookFinder.Bot.Models;

namespace CookFinder.Bot.Repositories;

public interface IRecipeRepository
{
    Task SaveAsync(Recipe recipe, CancellationToken cancellationToken);
    Task<IReadOnlyList<Recipe>> GetByCategoryAsync(long userId, string category, CancellationToken cancellationToken);
    Task<IReadOnlyList<Recipe>> SearchByNameAsync(long userId, string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<Recipe>> SearchByIngredientAsync(long userId, string ingredient, CancellationToken cancellationToken);
    Task<IReadOnlyList<Recipe>> GetFavoritesAsync(long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Recipe>> GetAllAsync(long userId, CancellationToken cancellationToken);
    Task<Recipe?> GetByIdAsync(long userId, string id, CancellationToken cancellationToken);
    Task<Recipe?> GetBySourceUrlAsync(long userId, string sourceUrl, CancellationToken cancellationToken);
    Task SetFavoriteAsync(long userId, string id, bool isFavorite, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetCategoriesAsync(long userId, CancellationToken cancellationToken);
}
