namespace CookFinder.Bot.Services.Metadata;

public sealed class StubVideoMetadataClient(ILogger<StubVideoMetadataClient> logger) : IVideoMetadataClient
{
    public Task<VideoMetadata> GetMetadataAsync(Uri sourceUrl, CancellationToken cancellationToken)
    {
        logger.LogInformation("Stub metadata fetch for {Url}", sourceUrl);

        var metadata = new VideoMetadata(
            sourceUrl,
            $"Recipe video from {sourceUrl.Host}",
            "Video metadata extraction is not wired yet. Replace with platform API calls.",
            sourceUrl.Host);

        return Task.FromResult(metadata);
    }
}
