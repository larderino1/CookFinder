using CookFinder.Bot.Models;

namespace CookFinder.Bot.Services.Recipes;

public interface IRecipeExtractor
{
    Task<RecipeDraft> ExtractAsync(long userId, Uri sourceUrl, CancellationToken cancellationToken);
}

public sealed record RecipeDraft(
    long UserId,
    string SourceUrl,
    string Title,
    string Description,
    IReadOnlyList<Ingredient> Ingredients,
    IReadOnlyList<RecipeStep> Steps);
