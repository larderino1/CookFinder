using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CookFinder.Bot.Configurations;
using CookFinder.Bot.Models;
using Microsoft.Extensions.Options;

namespace CookFinder.Bot.Services.Recipes;

public sealed class OpenAiRecipeNutritionService(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiRecipeNutritionService> logger) : IRecipeNutritionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<NutritionInfo?> EstimateAsync(Recipe recipe, string language, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            logger.LogWarning("OpenAI API key is missing. Skipping nutrition estimation.");
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

        var prompt = string.Join(Environment.NewLine, new[]
        {
            "You are a nutrition assistant.",
            $"Target language: {language}.",
            "Estimate nutrition per serving using the ingredients list.",
            "Return JSON with fields: calories, protein, carbs, fat (all numbers).",
            $"Title: {recipe.Title}",
            $"Ingredients: {string.Join("; ", recipe.Ingredients.Select(item => $"{item.Name} ({item.Quantity})"))}"
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
            logger.LogWarning("OpenAI nutrition failed with status {StatusCode}.", response.StatusCode);
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseContent, JsonOptions);
        var content = chatResponse?.Choices.FirstOrDefault()?.Message.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            logger.LogWarning("OpenAI nutrition response was empty.");
            return null;
        }

        var jsonContent = ExtractJson(content);
        decimal calories;
        decimal protein;
        decimal carbs;
        decimal fat;

        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            if (!TryReadDecimal(root, "calories", out calories) ||
                !TryReadDecimal(root, "protein", out protein) ||
                !TryReadDecimal(root, "carbs", out carbs) ||
                !TryReadDecimal(root, "fat", out fat))
            {
                logger.LogWarning("OpenAI nutrition response contained invalid values.");
                return null;
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "OpenAI nutrition response could not be parsed.");
            return null;
        }

        return new NutritionInfo(calories, protein, carbs, fat);
    }

    private static bool TryReadDecimal(JsonElement root, string propertyName, out decimal result)
    {
        result = 0;
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDecimal(out result),
            JsonValueKind.String => decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out result),
            _ => false
        };
    }

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice> Choices);

    private sealed record ChatChoice(ChatMessage Message);

    private sealed record ChatMessage(string Content);

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
