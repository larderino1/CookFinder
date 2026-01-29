using CookFinder.Bot.Configurations;
using Microsoft.Extensions.Options;

namespace CookFinder.Bot.Services;

public sealed class KeywordRecipeCategorizer(IOptions<RecipeOptions> options) : IRecipeCategorizer
{
    private readonly IReadOnlyList<string> _defaultCategories = options.Value.DefaultCategories;

    public Task<IReadOnlyList<string>> CategorizeAsync(string title, string description, CancellationToken cancellationToken)
    {
        var haystack = $"{title} {description}".ToLowerInvariant();
        var matches = new List<string>();

        if (haystack.Contains("breakfast") || haystack.Contains("eggs"))
        {
            matches.Add("Breakfast");
        }

        if (haystack.Contains("lunch") || haystack.Contains("sandwich"))
        {
            matches.Add("Lunch");
        }

        if (haystack.Contains("dinner") || haystack.Contains("pasta"))
        {
            matches.Add("Dinner");
        }

        if (haystack.Contains("snack") || haystack.Contains("chips"))
        {
            matches.Add("Snack");
        }

        if (haystack.Contains("dessert") || haystack.Contains("cake") || haystack.Contains("cookie"))
        {
            matches.Add("Dessert");
        }

        if (haystack.Contains("drink") || haystack.Contains("smoothie") || haystack.Contains("coffee"))
        {
            matches.Add("Drinks");
        }

        if (matches.Count == 0)
        {
            matches.Add(_defaultCategories.FirstOrDefault() ?? "Uncategorized");
        }

        return Task.FromResult<IReadOnlyList<string>>(matches.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }
}
