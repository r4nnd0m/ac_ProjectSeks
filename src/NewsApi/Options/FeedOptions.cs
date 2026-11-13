namespace NewsApi.Options;

public class FeedOptions
{
    public const string SectionName = "Feeds";

    public List<string> Urls { get; set; } = [];

    public IEnumerable<string> ActiveUrls => Urls.Where(u => !string.IsNullOrWhiteSpace(u));

    public static bool IsAllowedUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps;
}
