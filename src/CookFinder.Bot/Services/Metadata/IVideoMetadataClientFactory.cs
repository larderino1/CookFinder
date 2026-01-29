namespace CookFinder.Bot.Services;

public interface IVideoMetadataClientFactory
{
    IVideoMetadataClient GetClient(Uri sourceUrl);
}
