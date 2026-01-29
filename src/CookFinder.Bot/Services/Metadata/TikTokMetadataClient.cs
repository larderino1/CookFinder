using System.Text.Json;

namespace CookFinder.Bot.Services.Metadata;

public sealed class TikTokMetadataClient(HttpClient httpClient, ILogger<TikTokMetadataClient> logger) : IVideoMetadataClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<VideoMetadata> GetMetadataAsync(Uri sourceUrl, CancellationToken cancellationToken)
    {
        var requestUri = $"https://www.tiktok.com/oembed?url={Uri.EscapeDataString(sourceUrl.ToString())}";
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<TikTokOEmbedResponse>(content, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse TikTok oEmbed response.");

        var title = string.IsNullOrWhiteSpace(payload.Title)
            ? $"TikTok recipe from {sourceUrl.Host}"
            : payload.Title;
        var author = string.IsNullOrWhiteSpace(payload.AuthorName) ? "TikTok" : payload.AuthorName;

        var metadata = new VideoMetadata(
            sourceUrl,
            title,
            payload.AuthorUrl ?? string.Empty,
            author);

        logger.LogInformation("TikTok metadata fetched via oEmbed for {Url}.", sourceUrl);
        return metadata;
    }

    private sealed record TikTokOEmbedResponse(string? Title, string? AuthorName, string? AuthorUrl);
}
