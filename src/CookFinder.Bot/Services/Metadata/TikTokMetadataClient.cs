using CookFinder.Bot.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CookFinder.Bot.Services;

public sealed class TikTokMetadataClient(IOptions<VideoSourceOptions> options, ILogger<TikTokMetadataClient> logger) : IVideoMetadataClient
{
    public Task<VideoMetadata> GetMetadataAsync(Uri sourceUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.TikTokApiKey))
        {
            throw new InvalidOperationException("TikTok API key is missing.");
        }

        logger.LogInformation("TikTok metadata stub for {Url}.", sourceUrl);

        var metadata = new VideoMetadata(
            sourceUrl,
            $"TikTok recipe from {sourceUrl.Host}",
            "Replace this stub with TikTok API or approved data source.",
            "TikTok");

        return Task.FromResult(metadata);
    }
}
