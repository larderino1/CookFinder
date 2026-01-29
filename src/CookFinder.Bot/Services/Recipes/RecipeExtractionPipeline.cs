using CookFinder.Bot.Services.Metadata;
using CookFinder.Bot.Services.Parsing;

namespace CookFinder.Bot.Services.Recipes;

public sealed class RecipeExtractionPipeline(
    IVideoMetadataClientFactory metadataClientFactory,
    IRecipeParser parser,
    IRecipeParser fallbackParser,
    ILogger<RecipeExtractionPipeline> logger) : IRecipeExtractor
{
    public async Task<RecipeDraft> ExtractAsync(long userId, Uri sourceUrl, CancellationToken cancellationToken)
    {
        var metadataClient = metadataClientFactory.GetClient(sourceUrl);
        var metadata = await metadataClient.GetMetadataAsync(sourceUrl, cancellationToken);

        ParsedRecipe parsed;
        try
        {
            parsed = await parser.ParseAsync(metadata, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Primary parser failed, falling back to rule-based parser.");
            parsed = await fallbackParser.ParseAsync(metadata, cancellationToken);
        }

        return new RecipeDraft(
            userId,
            metadata.SourceUrl.ToString(),
            parsed.Title,
            parsed.Description,
            parsed.Ingredients,
            parsed.Steps);
    }
}
