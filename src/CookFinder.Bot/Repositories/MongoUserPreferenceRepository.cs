using CookFinder.Bot.Configurations;
using CookFinder.Bot.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CookFinder.Bot.Repositories;

public sealed class MongoUserPreferenceRepository : IUserPreferenceRepository
{
    private readonly IMongoCollection<UserPreference> _preferences;

    public MongoUserPreferenceRepository(IOptions<MongoOptions> options)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        var database = client.GetDatabase(options.Value.Database);
        _preferences = database.GetCollection<UserPreference>("user_preferences");
    }

    public async Task<string> GetLanguageAsync(long userId, CancellationToken cancellationToken)
    {
        var preference = await _preferences
            .Find(pref => pref.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(preference?.Language) ? "en" : preference!.Language;
    }

    public async Task SetLanguageAsync(long userId, string language, CancellationToken cancellationToken)
    {
        var filter = Builders<UserPreference>.Filter.Eq(pref => pref.UserId, userId);
        var update = Builders<UserPreference>.Update.Set(pref => pref.Language, language);

        await _preferences.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }
}
