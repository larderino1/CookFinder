using CookFinder.Bot.Models;

namespace CookFinder.Bot.Services;

public interface IRecipeParser
{
    Task<ParsedRecipe> ParseAsync(VideoMetadata metadata, CancellationToken cancellationToken);
}

public sealed record ParsedRecipe(
    string Title,
    string Description,
    IReadOnlyList<Ingredient> Ingredients,
    IReadOnlyList<RecipeStep> Steps);
