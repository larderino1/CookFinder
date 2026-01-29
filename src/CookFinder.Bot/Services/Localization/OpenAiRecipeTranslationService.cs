using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CookFinder.Bot.Configurations;
using CookFinder.Bot.Models;
using Microsoft.Extensions.Options;

namespace CookFinder.Bot.Services.Localization;

public sealed class OpenAiRecipeTranslationService(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiRecipeTranslationService> logger) : IRecipeTranslationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TranslatedRecipe> TranslateAsync(
        Recipe recipe,
        IReadOnlyList<string> categories,
        string language,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(language) || language.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            return new TranslatedRecipe(recipe.Title, recipe.Description, recipe.Ingredients, recipe.Steps, categories);
        }

        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            logger.LogWarning("OpenAI API key is missing. Skipping translation.");
            return new TranslatedRecipe(recipe.Title, recipe.Description, recipe.Ingredients, recipe.Steps, categories);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

        var prompt = string.Join(Environment.NewLine, new[]
        {
            "You are a translation assistant for recipes.",
            $"Target language: {language}.",
            "Translate the recipe title, description, ingredients (name and quantity), steps, and categories.",
            "Return JSON with fields: title, description, ingredients (array of {name, quantity}), steps (array of {order, instruction}), categories (array of strings).",
            "Validate the translation: keep quantities and proper nouns unchanged when appropriate.",
            $"Title: {recipe.Title}",
            $"Description: {recipe.Description}",
            $"Ingredients: {string.Join("; ", recipe.Ingredients.Select(item => $"{item.Name} ({item.Quantity})"))}",
            $"Steps: {string.Join(" | ", recipe.Steps.OrderBy(step => step.Order).Select(step => $"{step.Order}. {step.Instruction}"))}",
            $"Categories: {string.Join(", ", categories)}"
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
            logger.LogWarning("OpenAI translation failed with status {StatusCode}.", response.StatusCode);
            return new TranslatedRecipe(recipe.Title, recipe.Description, recipe.Ingredients, recipe.Steps, categories);
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseContent, JsonOptions);
        var content = chatResponse?.Choices.FirstOrDefault()?.Message.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            logger.LogWarning("OpenAI translation response was empty.");
            return new TranslatedRecipe(recipe.Title, recipe.Description, recipe.Ingredients, recipe.Steps, categories);
        }

        var jsonContent = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<TranslationResponse>(jsonContent, JsonOptions);

        if (parsed is null)
        {
            logger.LogWarning("OpenAI translation response could not be parsed.");
            return new TranslatedRecipe(recipe.Title, recipe.Description, recipe.Ingredients, recipe.Steps, categories);
        }

        if (!HasValidIngredients(parsed.Ingredients, recipe.Ingredients) || !HasValidSteps(parsed.Steps, recipe.Steps))
        {
            logger.LogWarning("OpenAI translation validation failed. Keeping original recipe text.");
            return new TranslatedRecipe(recipe.Title, recipe.Description, recipe.Ingredients, recipe.Steps, categories);
        }

        var translatedIngredients = parsed.Ingredients.Select(item => new Ingredient
        {
            Name = item.Name ?? string.Empty,
            Quantity = item.Quantity
        }).ToList();

        var translatedSteps = parsed.Steps.Select(step => new RecipeStep
        {
            Order = step.Order,
            Instruction = step.Instruction ?? string.Empty
        }).ToList();

        var translatedCategories = parsed.Categories?.Where(category => !string.IsNullOrWhiteSpace(category))
            .Select(category => category.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? categories.ToList();

        return new TranslatedRecipe(
            parsed.Title ?? recipe.Title,
            parsed.Description ?? recipe.Description,
            translatedIngredients,
            translatedSteps,
            translatedCategories);
    }

    private static bool HasValidIngredients(IReadOnlyList<IngredientJson>? translated, IReadOnlyList<Ingredient> original)
    {
        if (translated is null || translated.Count != original.Count)
        {
            return false;
        }

        return translated.All(item => !string.IsNullOrWhiteSpace(item.Name));
    }

    private static bool HasValidSteps(IReadOnlyList<StepJson>? translated, IReadOnlyList<RecipeStep> original)
    {
        if (translated is null || translated.Count != original.Count)
        {
            return false;
        }

        var ordered = translated.OrderBy(step => step.Order).ToList();
        return ordered.Select((step, index) => step.Order == original[index].Order && !string.IsNullOrWhiteSpace(step.Instruction)).All(valid => valid);
    }

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice> Choices);

    private sealed record ChatChoice(ChatMessage Message);

    private sealed record ChatMessage(string Content);

    private sealed record TranslationResponse(
        string? Title,
        string? Description,
        IReadOnlyList<IngredientJson>? Ingredients,
        IReadOnlyList<StepJson>? Steps,
        IReadOnlyList<string>? Categories);

    private sealed record IngredientJson(string? Name, string? Quantity);

    private sealed record StepJson(int Order, string? Instruction);

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
