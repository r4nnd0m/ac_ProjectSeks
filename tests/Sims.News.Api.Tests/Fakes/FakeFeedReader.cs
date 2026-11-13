using NewsApi.Models;
using NewsApi.Services;

namespace Sims.News.Api.Tests.Fakes;

public class FakeFeedReader : IFeedReader
{
    private readonly Dictionary<string, IReadOnlyList<NewsItem>> _feeds = new();
    private readonly HashSet<string> _broken = new();

    public FakeFeedReader WithFeed(string url, params NewsItem[] items)
    {
        _feeds[url] = items;
        return this;
    }

    public FakeFeedReader WithBrokenFeed(string url)
    {
        _broken.Add(url);
        return this;
    }

    public Task<IReadOnlyList<NewsItem>> ReadAsync(string url, CancellationToken ct)
    {
        if (_broken.Contains(url))
        {
            throw new HttpRequestException("Feed nicht erreichbar (Test)");
        }

        return Task.FromResult(_feeds.TryGetValue(url, out var items) ? items : (IReadOnlyList<NewsItem>)[]);
    }
}
