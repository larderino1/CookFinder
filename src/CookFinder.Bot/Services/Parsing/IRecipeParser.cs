using CookFinder.Bot.Models;
using CookFinder.Bot.Services.Metadata;

namespace CookFinder.Bot.Services.Parsing;

public interface IRecipeParser
{
    Task<ParsedRecipe> ParseAsync(VideoMetadata metadata, CancellationToken cancellationToken);
}

public sealed record ParsedRecipe(
    string Title,
    string Description,
    IReadOnlyList<Ingredient> Ingredients,
    IReadOnlyList<RecipeStep> Steps);
