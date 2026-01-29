using CookFinder.Bot.Models;
using Microsoft.Extensions.Logging;

namespace CookFinder.Bot.Services;

public sealed class RuleBasedRecipeParser(ILogger<RuleBasedRecipeParser> logger) : IRecipeParser
{
    public Task<ParsedRecipe> ParseAsync(VideoMetadata metadata, CancellationToken cancellationToken)
    {
        logger.LogInformation("Rule-based parsing for {Url}", metadata.SourceUrl);

        var ingredients = new List<Ingredient>
        {
            new() { Name = "Ingredient placeholder", Quantity = "1" }
        };

        var steps = new List<RecipeStep>
        {
            new() { Order = 1, Instruction = "Review the video and write the steps." }
        };

        var parsed = new ParsedRecipe(
            metadata.Title,
            metadata.Description,
            ingredients,
            steps);

        return Task.FromResult(parsed);
    }
}
