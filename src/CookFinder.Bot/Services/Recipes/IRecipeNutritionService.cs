using CookFinder.Bot.Models;

namespace CookFinder.Bot.Services.Recipes;

public interface IRecipeNutritionService
{
    Task<NutritionInfo?> EstimateAsync(Recipe recipe, string language, CancellationToken cancellationToken);
}
