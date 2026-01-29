using System.Text.Json;
using CookFinder.Bot.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CookFinder.Bot.Services;

public sealed class YouTubeMetadataClient(HttpClient httpClient, IOptions<VideoSourceOptions> options, ILogger<YouTubeMetadataClient> logger) : IVideoMetadataClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<VideoMetadata> GetMetadataAsync(Uri sourceUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.YouTubeApiKey))
        {
            throw new InvalidOperationException("YouTube API key is missing.");
        }

        var videoId = ExtractVideoId(sourceUrl);
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new InvalidOperationException("Unable to extract YouTube video ID.");
        }

        var requestUri = $"https://www.googleapis.com/youtube/v3/videos?part=snippet&id={videoId}&key={options.Value.YouTubeApiKey}";
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<YouTubeVideoResponse>(content, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse YouTube response.");

        var snippet = payload.Items?.FirstOrDefault()?.Snippet
            ?? throw new InvalidOperationException("YouTube response did not include snippet data.");

        var metadata = new VideoMetadata(
            sourceUrl,
            snippet.Title ?? $"YouTube recipe from {sourceUrl.Host}",
            snippet.Description ?? string.Empty,
            snippet.ChannelTitle ?? "YouTube");

        logger.LogInformation("YouTube metadata fetched for {Url}.", sourceUrl);
        return metadata;
    }

    private static string? ExtractVideoId(Uri sourceUrl)
    {
        if (sourceUrl.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            return sourceUrl.AbsolutePath.Trim('/');
        }

        var query = sourceUrl.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var kvp = part.Split('=', 2);
            if (kvp.Length == 2 && kvp[0].Equals("v", StringComparison.OrdinalIgnoreCase))
            {
                return kvp[1];
            }
        }

        return null;
    }

    private sealed record YouTubeVideoResponse(IReadOnlyList<YouTubeVideoItem>? Items);

    private sealed record YouTubeVideoItem(YouTubeVideoSnippet? Snippet);

    private sealed record YouTubeVideoSnippet(string? Title, string? Description, string? ChannelTitle);
}
