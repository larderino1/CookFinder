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
        logger.LogInformation("Recipe extraction started for user {UserId} and URL {Url}.", userId, sourceUrl);

        var metadataClient = metadataClientFactory.GetClient(sourceUrl);
        logger.LogInformation("Metadata client selected: {ClientType}.", metadataClient.GetType().Name);

        var metadata = await metadataClient.GetMetadataAsync(sourceUrl, cancellationToken);
        logger.LogInformation(
            "Metadata fetched for URL {Url}. TitleLength={TitleLength}, DescriptionLength={DescriptionLength}, Author={Author}.",
            sourceUrl,
            metadata.Title.Length,
            metadata.Description.Length,
            metadata.Author);

        ParsedRecipe parsed;
        try
        {
            parsed = await parser.ParseAsync(metadata, cancellationToken);
            logger.LogInformation("Primary parser succeeded for URL {Url}.", sourceUrl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Primary parser failed, falling back to rule-based parser.");
            parsed = await fallbackParser.ParseAsync(metadata, cancellationToken);
            logger.LogInformation("Fallback parser succeeded for URL {Url}.", sourceUrl);
        }

        logger.LogInformation(
            "Recipe extraction finished for user {UserId}. ParsedTitleLength={TitleLength}, Ingredients={IngredientCount}, Steps={StepCount}.",
            userId,
            parsed.Title.Length,
            parsed.Ingredients.Count,
            parsed.Steps.Count);

        return new RecipeDraft(
            userId,
            metadata.SourceUrl.ToString(),
            parsed.Title,
            parsed.Description,
            parsed.Ingredients,
            parsed.Steps);
    }
}
