using CookFinder.Bot.Models;
using Microsoft.Extensions.Logging;

namespace CookFinder.Bot.Services;

public sealed class StubRecipeExtractor(ILogger<StubRecipeExtractor> logger) : IRecipeExtractor
{
    public Task<RecipeDraft> ExtractAsync(long userId, Uri sourceUrl, CancellationToken cancellationToken)
    {
        logger.LogInformation("Stub extraction for {Url}", sourceUrl);

        var title = $"Recipe from {sourceUrl.Host}";
        var ingredients = new List<Ingredient>
        {
            new() { Name = "Ingredient from video", Quantity = "1" }
        };
        var steps = new List<RecipeStep>
        {
            new() { Order = 1, Instruction = "Watch the video and capture steps." }
        };

        var draft = new RecipeDraft(
            userId,
            sourceUrl.ToString(),
            title,
            "Auto-generated draft. Replace with parsed recipe text.",
            ingredients,
            steps);

        return Task.FromResult(draft);
    }
}
