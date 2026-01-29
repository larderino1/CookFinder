namespace CookFinder.Bot.Services.Metadata;

public interface IVideoMetadataClientFactory
{
    IVideoMetadataClient GetClient(Uri sourceUrl);
}
