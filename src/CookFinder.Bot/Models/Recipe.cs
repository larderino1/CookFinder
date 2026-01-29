using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CookFinder.Bot.Models;

public sealed class Recipe
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; init; }

    public long UserId { get; init; }
    public string SourceUrl { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<Ingredient> Ingredients { get; init; } = Array.Empty<Ingredient>();
    public IReadOnlyList<RecipeStep> Steps { get; init; } = Array.Empty<RecipeStep>();
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
