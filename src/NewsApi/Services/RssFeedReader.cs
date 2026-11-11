using System.ServiceModel.Syndication;
using System.Xml;
using NewsApi.Models;

namespace NewsApi.Services;

public class RssFeedReader : IFeedReader
{
    private readonly HttpClient _http;

    public RssFeedReader(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<NewsItem>> ReadAsync(string url, CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 5_000_000,
            Async = true
        };

        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using XmlReader xml = XmlReader.Create(stream, settings);
        SyndicationFeed feed = SyndicationFeed.Load(xml);

        string source = feed.Title?.Text ?? new Uri(url).Host;

        var items = new List<NewsItem>();
        foreach (SyndicationItem item in feed.Items)
        {
            NewsItem? news = ToNewsItem(item, source);
            if (news != null)
            {
                items.Add(news);
            }
        }
        return items;
    }

    public static NewsItem? ToNewsItem(SyndicationItem item, string source)
    {
        string? title = item.Title?.Text;
        string? link = item.Links.FirstOrDefault()?.Uri?.ToString();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        DateTimeOffset published = item.PublishDate != DateTimeOffset.MinValue
            ? item.PublishDate
            : item.LastUpdatedTime;

        return new NewsItem(title, link, source, published);
    }
}
