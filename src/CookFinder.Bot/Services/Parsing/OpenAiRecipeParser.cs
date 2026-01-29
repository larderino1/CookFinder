using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CookFinder.Bot.Configurations;
using CookFinder.Bot.Models;
using CookFinder.Bot.Services.Metadata;
using Microsoft.Extensions.Options;

namespace CookFinder.Bot.Services.Parsing;

public sealed class OpenAiRecipeParser(HttpClient httpClient, IOptions<OpenAiOptions> options) : IRecipeParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ParsedRecipe> ParseAsync(VideoMetadata metadata, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is missing.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

        var prompt = string.Join(Environment.NewLine, new[]
        {
            "You are a recipe assistant. Turn the video metadata into a concise recipe with ingredients and numbered steps.",
            $"Title: {metadata.Title}",
            $"Description: {metadata.Description}",
            $"Author: {metadata.Author}",
            "Return JSON with fields: title, description, ingredients (array of {name, quantity}), steps (array of {order, instruction})."
        });

        var payload = new
        {
            model = options.Value.Model,
            messages = new[]
            {
                new { role = "system", content = "You output only JSON." },
                new { role = "user", content = prompt }
            },
            temperature = 0.3
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseContent, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse OpenAI response.");

        var content = chatResponse.Choices.FirstOrDefault()?.Message.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenAI response was empty.");
        }

        var jsonContent = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<RecipeJson>(jsonContent, JsonOptions)
            ?? throw new InvalidOperationException("OpenAI response JSON did not match schema.");

        return new ParsedRecipe(
            parsed.Title ?? metadata.Title,
            parsed.Description ?? metadata.Description,
            parsed.Ingredients?.Select(item => new Ingredient { Name = item.Name ?? "", Quantity = item.Quantity }).ToList(),
            parsed.Steps?.Select(step => new RecipeStep { Order = step.Order, Instruction = step.Instruction ?? "" }).ToList());
    }
    
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

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice> Choices);

    private sealed record ChatChoice(ChatMessage Message);

    private sealed record ChatMessage(string Content);

    private sealed record RecipeJson(
        string? Title,
        string? Description,
        IReadOnlyList<IngredientJson>? Ingredients,
        IReadOnlyList<StepJson>? Steps);

    private sealed record IngredientJson(string? Name, string? Quantity);

    private sealed record StepJson(int Order, string? Instruction);
}
