using CookFinder.Bot.Configurations;
using CookFinder.Bot.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CookFinder.Bot.Repositories;

public sealed class MongoRecipeRepository : IRecipeRepository
{
    private readonly IMongoCollection<Recipe> _recipes;

    public MongoRecipeRepository(IOptions<MongoOptions> options)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        var database = client.GetDatabase(options.Value.Database);
        _recipes = database.GetCollection<Recipe>("recipes");
    }

    public async Task SaveAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        await _recipes.InsertOneAsync(recipe, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> GetByCategoryAsync(long userId, string category, CancellationToken cancellationToken)
    {
        var filter = Builders<Recipe>.Filter.And(
            Builders<Recipe>.Filter.Eq(r => r.UserId, userId),
            Builders<Recipe>.Filter.AnyEq(r => r.Categories, category));

        return await _recipes.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> SearchByNameAsync(long userId, string name, CancellationToken cancellationToken)
    {
        var filter = Builders<Recipe>.Filter.And(
            Builders<Recipe>.Filter.Eq(r => r.UserId, userId),
            Builders<Recipe>.Filter.Regex(r => r.Title, new MongoDB.Bson.BsonRegularExpression(name, "i")));

        return await _recipes.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> SearchByIngredientAsync(long userId, string ingredient, CancellationToken cancellationToken)
    {
        var filter = Builders<Recipe>.Filter.And(
            Builders<Recipe>.Filter.Eq(r => r.UserId, userId),
            Builders<Recipe>.Filter.ElemMatch(r => r.Ingredients,
                Builders<Ingredient>.Filter.Regex(i => i.Name, new MongoDB.Bson.BsonRegularExpression(ingredient, "i"))));

        return await _recipes.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> GetFavoritesAsync(long userId, CancellationToken cancellationToken)
    {
        var filter = Builders<Recipe>.Filter.And(
            Builders<Recipe>.Filter.Eq(r => r.UserId, userId),
            Builders<Recipe>.Filter.Eq(r => r.IsFavorite, true));

        return await _recipes.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> GetAllAsync(long userId, CancellationToken cancellationToken)
    {
        var filter = Builders<Recipe>.Filter.Eq(r => r.UserId, userId);
        return await _recipes.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<Recipe?> GetByIdAsync(long userId, string id, CancellationToken cancellationToken)
    {
        var filter = Builders<Recipe>.Filter.And(
            Builders<Recipe>.Filter.Eq(r => r.UserId, userId),
            Builders<Recipe>.Filter.Eq(r => r.Id, id));

        return await _recipes.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Recipe?> GetBySourceUrlAsync(long userId, string sourceUrl, CancellationToken cancellationToken)
    {
        var filter = Builders<Recipe>.Filter.And(
            Builders<Recipe>.Filter.Eq(r => r.UserId, userId),
            Builders<Recipe>.Filter.Eq(r => r.SourceUrl, sourceUrl));

        return await _recipes.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetFavoriteAsync(long userId, string id, bool isFavorite, CancellationToken cancellationToken)
    {
        var filter = Builders<Recipe>.Filter.And(
            Builders<Recipe>.Filter.Eq(r => r.UserId, userId),
            Builders<Recipe>.Filter.Eq(r => r.Id, id));
        var update = Builders<Recipe>.Update.Set(r => r.IsFavorite, isFavorite);
        await _recipes.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(long userId, CancellationToken cancellationToken)
    {
        var filter = Builders<Recipe>.Filter.Eq(r => r.UserId, userId);
        var projection = Builders<Recipe>.Projection.Expression(r => r.Categories);
        var categories = await _recipes.Find(filter).Project(projection).ToListAsync(cancellationToken);

        return categories.SelectMany(list => list).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
