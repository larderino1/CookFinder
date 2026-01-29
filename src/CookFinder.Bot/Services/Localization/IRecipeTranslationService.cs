using CookFinder.Bot.Models;

namespace CookFinder.Bot.Services.Localization;

public interface IRecipeTranslationService
{
    Task<TranslatedRecipe> TranslateAsync(Recipe recipe, IReadOnlyList<string> categories, string language, CancellationToken cancellationToken);
}

public sealed record TranslatedRecipe(
    string Title,
    string Description,
    IReadOnlyList<Ingredient> Ingredients,
    IReadOnlyList<RecipeStep> Steps,
    IReadOnlyList<string> Categories);
