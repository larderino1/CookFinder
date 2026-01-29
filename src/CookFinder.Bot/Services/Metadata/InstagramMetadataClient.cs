using System.Text.Encodings.Web;
using System.Text.Json;
using CookFinder.Bot.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CookFinder.Bot.Services;

public sealed class InstagramMetadataClient(HttpClient httpClient, IOptions<VideoSourceOptions> options, ILogger<InstagramMetadataClient> logger) : IVideoMetadataClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<VideoMetadata> GetMetadataAsync(Uri sourceUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.InstagramAccessToken))
        {
            throw new InvalidOperationException("Instagram access token is missing.");
        }

        var encodedUrl = UrlEncoder.Default.Encode(sourceUrl.ToString());
        var requestUri = $"https://graph.facebook.com/v19.0/instagram_oembed?url={encodedUrl}&access_token={options.Value.InstagramAccessToken}";
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<InstagramOembedResponse>(content, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse Instagram response.");

        var metadata = new VideoMetadata(
            sourceUrl,
            payload.Title ?? $"Instagram recipe from {sourceUrl.Host}",
            string.Empty,
            payload.AuthorName ?? "Instagram");

        logger.LogInformation("Instagram metadata fetched for {Url}.", sourceUrl);
        return metadata;
    }

    private sealed record InstagramOembedResponse(string? Title, string? AuthorName);
}
