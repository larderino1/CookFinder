namespace CookFinder.Bot.Models;

public sealed class RecipeStep
{
    public int Order { get; init; }
    public string Instruction { get; init; } = string.Empty;
}
