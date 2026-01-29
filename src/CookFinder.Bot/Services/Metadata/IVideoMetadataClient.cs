namespace CookFinder.Bot.Services.Metadata;

public interface IVideoMetadataClient
{
    Task<VideoMetadata> GetMetadataAsync(Uri sourceUrl, CancellationToken cancellationToken);
}

public sealed record VideoMetadata(
    Uri SourceUrl,
    string Title,
    string Description,
    string Author);
