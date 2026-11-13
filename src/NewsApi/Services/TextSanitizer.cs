using System.Net;
using System.Text.RegularExpressions;

namespace NewsApi.Services;

// Entfernt HTML aus Feed-Texten und lässt nur http(s)-Links durch.
public static partial class TextSanitizer
{
    public static string Clean(string? text, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string decoded = WebUtility.HtmlDecode(text);
        string noTags = TagRegex().Replace(decoded, " ");
        string compact = SpaceRegex().Replace(noTags, " ").Trim();

        return compact.Length <= maxLength
            ? compact
            : compact[..(maxLength - 1)].TrimEnd() + "…";
    }

    public static bool IsSafeLink(string link)
    {
        return Uri.TryCreate(link, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();
}
