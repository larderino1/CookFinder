namespace CookFinder.Bot.Models;

public sealed class Ingredient
{
    public string Name { get; init; } = string.Empty;
    public string? Quantity { get; init; }
}
