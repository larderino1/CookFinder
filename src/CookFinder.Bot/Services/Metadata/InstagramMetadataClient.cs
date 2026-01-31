using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using CookFinder.Bot.Configurations;
using Microsoft.Extensions.Options;

namespace CookFinder.Bot.Services.Metadata;

public sealed class InstagramMetadataClient(HttpClient httpClient, IOptions<VideoSourceOptions> options, ILogger<InstagramMetadataClient> logger) : IVideoMetadataClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string GraphqlDocId = "10015901848480474";
    private const string GraphqlLsd = "AVqbxe3J_YA";
    private const string AsbdId = "129477";
    private static readonly Regex InstagramIdRegex = new(
        @"instagram\.com\/(?:[A-Za-z0-9_.]+\/)?(p|reels|reel|stories)\/([A-Za-z0-9-_]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<VideoMetadata> GetMetadataAsync(Uri sourceUrl, CancellationToken cancellationToken)
    {
        EnsureInstagramHeadersConfigured();

        var shortcode = GetShortcode(sourceUrl);
        var graphqlUri = BuildGraphqlUri(shortcode);
        using var request = new HttpRequestMessage(HttpMethod.Post, graphqlUri);
        request.Headers.TryAddWithoutValidation("User-Agent", options.Value.InstagramUserAgent);
        request.Headers.TryAddWithoutValidation("X-IG-App-ID", options.Value.InstagramAppId);
        request.Headers.TryAddWithoutValidation("X-FB-LSD", GraphqlLsd);
        request.Headers.TryAddWithoutValidation("X-ASBD-ID", AsbdId);
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        request.Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!IsJsonResponse(response, content))
        {
            var preview = content.Length > 200 ? content[..200] : content;
            logger.LogWarning("Instagram response was not JSON. Content-Type: {ContentType}. Preview: {Preview}", response.Content.Headers.ContentType?.MediaType, preview);
            throw new InvalidOperationException("Instagram response was not JSON. Check headers like User-Agent and X-IG-App-ID.");
        }

        using var document = JsonDocument.Parse(content);
        if (!TryGetElement(document.RootElement, out var media, "data", "xdt_shortcode_media"))
        {
            throw new InvalidOperationException("Failed to parse Instagram response.");
        }

        var caption = GetCaption(media);
        var author = GetOwnerName(media) ?? "Instagram";
        var title = !string.IsNullOrWhiteSpace(caption)
            ? caption
            : $"Instagram recipe from {sourceUrl.Host}";

        var metadata = new VideoMetadata(
            sourceUrl,
            title,
            caption ?? string.Empty,
            author);

        logger.LogInformation("Instagram metadata fetched for {Url}.", sourceUrl);
        return metadata;
    }

    private void EnsureInstagramHeadersConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.Value.InstagramUserAgent) ||
            string.IsNullOrWhiteSpace(options.Value.InstagramAppId))
        {
            throw new InvalidOperationException("Instagram headers are missing.");
        }
    }

    private static string GetShortcode(Uri sourceUrl)
    {
        var match = InstagramIdRegex.Match(sourceUrl.ToString());
        if (!match.Success || match.Groups.Count < 3)
        {
            throw new InvalidOperationException("Invalid Instagram URL.");
        }

        return match.Groups[2].Value;
    }

    private static Uri BuildGraphqlUri(string shortcode)
    {
        var variables = JsonSerializer.Serialize(new { shortcode }, JsonOptions);
        var query = $"variables={UrlEncoder.Default.Encode(variables)}&doc_id={GraphqlDocId}&lsd={GraphqlLsd}";
        return new UriBuilder("https://www.instagram.com/api/graphql") { Query = query }.Uri;
    }

    private static string? GetCaption(JsonElement media)
    {
        if (!TryGetElement(media, out var captionElement, "edge_media_to_caption") ||
            !captionElement.TryGetProperty("edges", out var edges) ||
            edges.ValueKind != JsonValueKind.Array ||
            edges.GetArrayLength() == 0)
        {
            return null;
        }

        var firstEdge = edges[0];
        if (TryGetElement(firstEdge, out var node, "node") &&
            node.TryGetProperty("text", out var textElement) &&
            textElement.ValueKind == JsonValueKind.String)
        {
            return textElement.GetString();
        }

        return null;
    }

    private static string? GetOwnerName(JsonElement media)
    {
        if (!TryGetElement(media, out var owner, "owner"))
        {
            return null;
        }

        if (owner.TryGetProperty("username", out var username) && username.ValueKind == JsonValueKind.String)
        {
            return username.GetString();
        }

        if (owner.TryGetProperty("full_name", out var fullName) && fullName.ValueKind == JsonValueKind.String)
        {
            return fullName.GetString();
        }

        return null;
    }

    private static bool TryGetElement(JsonElement element, out JsonElement value, params string[] path)
    {
        value = element;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsJsonResponse(HttpResponseMessage response, string content)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(mediaType) && mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return content.TrimStart().StartsWith('{');
    }
}
