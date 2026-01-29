namespace CookFinder.Bot.Configurations;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gpt-4o-mini";
    public string Endpoint { get; init; } = "https://api.openai.com/v1/chat/completions";
}
