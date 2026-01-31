namespace CookFinder.Bot.Configurations;

public sealed class VideoSourceOptions
{
    public const string SectionName = "VideoSources";

    public string YouTubeApiKey { get; init; } = string.Empty;
    public string InstagramAccessToken { get; init; } = string.Empty;
    public string InstagramUserAgent { get; init; } = string.Empty;
    public string InstagramAppId { get; init; } = string.Empty;
    public string TikTokApiKey { get; init; } = string.Empty;
}
