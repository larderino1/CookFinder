using MongoDB.Bson.Serialization.Attributes;

namespace CookFinder.Bot.Models;

[BsonIgnoreExtraElements]
public sealed class UserPreference
{
    public long UserId { get; init; }
    public string Language { get; init; } = "en";
}
