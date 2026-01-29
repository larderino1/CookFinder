namespace CookFinder.Bot.Services;

public sealed class HostBasedVideoMetadataClientFactory(
    YouTubeMetadataClient youTubeClient,
    InstagramMetadataClient instagramClient,
    TikTokMetadataClient tikTokClient,
    StubVideoMetadataClient fallbackClient) : IVideoMetadataClientFactory
{
    public IVideoMetadataClient GetClient(Uri sourceUrl)
    {
        var host = sourceUrl.Host.ToLowerInvariant();

        if (host.Contains("youtube") || host.Contains("youtu.be"))
        {
            return youTubeClient;
        }

        if (host.Contains("instagram"))
        {
            return instagramClient;
        }

        if (host.Contains("tiktok"))
        {
            return tikTokClient;
        }

        return fallbackClient;
    }
}
