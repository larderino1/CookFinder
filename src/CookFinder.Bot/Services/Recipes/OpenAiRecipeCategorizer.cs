using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CookFinder.Bot.Configurations;
using Microsoft.Extensions.Options;

namespace CookFinder.Bot.Services.Recipes;

public sealed class OpenAiRecipeCategorizer(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    IOptions<RecipeOptions> recipeOptions,
    KeywordRecipeCategorizer fallbackCategorizer,
    ILogger<OpenAiRecipeCategorizer> logger) : IRecipeCategorizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<string> _defaultCategories = recipeOptions.Value.DefaultCategories;

    public async Task<IReadOnlyList<string>> CategorizeAsync(string title, string description, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            logger.LogWarning("OpenAI API key is missing. Falling back to keyword categorization.");
            return await fallbackCategorizer.CategorizeAsync(title, description, cancellationToken);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

        var categoryList = _defaultCategories.Count == 0
            ? "Uncategorized"
            : string.Join(", ", _defaultCategories);

        var prompt = string.Join(Environment.NewLine, new[]
        {
            "You are a culinary classification assistant.",
            "Choose up to 3 categories for the recipe using ONLY the provided categories.",
            $"Available categories: {categoryList}.",
            $"Title: {title}",
            $"Description: {description}",
            "Return JSON with field: categories (array of strings)."
        });

        var payload = new
        {
            model = options.Value.Model,
            messages = new[]
            {
                new { role = "system", content = "You output only JSON." },
                new { role = "user", content = prompt }
            },
            temperature = 0.2
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OpenAI categorization failed with status {StatusCode}.", response.StatusCode);
            return await fallbackCategorizer.CategorizeAsync(title, description, cancellationToken);
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseContent, JsonOptions);
        var content = chatResponse?.Choices.FirstOrDefault()?.Message.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            logger.LogWarning("OpenAI categorization response was empty.");
            return await fallbackCategorizer.CategorizeAsync(title, description, cancellationToken);
        }

        var jsonContent = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<CategoryResponse>(jsonContent, JsonOptions);

        if (parsed?.Categories is null || parsed.Categories.Count == 0)
        {
            logger.LogWarning("OpenAI categorization returned no categories.");
            return await fallbackCategorizer.CategorizeAsync(title, description, cancellationToken);
        }

        var normalized = NormalizeCategories(parsed.Categories);
        if (normalized.Count == 0)
        {
            logger.LogWarning("OpenAI categorization returned categories outside the allowed list.");
            return await fallbackCategorizer.CategorizeAsync(title, description, cancellationToken);
        }

        return normalized;
    }

    private IReadOnlyList<string> NormalizeCategories(IReadOnlyList<string> categories)
    {
        if (_defaultCategories.Count == 0)
        {
            return categories.Where(category => !string.IsNullOrWhiteSpace(category))
                .Select(category => category.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var allowed = new HashSet<string>(_defaultCategories, StringComparer.OrdinalIgnoreCase);
        return categories
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Select(category => category.Trim())
            .Where(category => allowed.Contains(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice> Choices);

    private sealed record ChatChoice(ChatMessage Message);

    private sealed record ChatMessage(string Content);

    private sealed record CategoryResponse(IReadOnlyList<string> Categories);

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }
        }

        return trimmed.Trim();
    }
}
