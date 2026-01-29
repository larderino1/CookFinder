namespace CookFinder.Bot.Configurations;

public sealed class RecipeOptions
{
    public const string SectionName = "Recipe";

    public IReadOnlyList<string> DefaultCategories { get; init; } = Array.Empty<string>();
}
