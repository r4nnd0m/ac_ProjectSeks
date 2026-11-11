using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NewsApi.Models;
using NewsApi.Options;
using NewsApi.Services;
using Sims.News.Api.Tests.Fakes;

namespace Sims.News.Api.Tests;

public class NewsServiceTests
{
    private const string FeedA = "https://a.test/rss";
    private const string FeedB = "https://b.test/rss";

    private static NewsService CreateService(FakeFeedReader reader, params string[] urls)
    {
        return new NewsService(
            reader,
            new MemoryCache(new MemoryCacheOptions()),
            Microsoft.Extensions.Options.Options.Create(new FeedOptions { Urls = urls.ToList() }),
            Microsoft.Extensions.Options.Options.Create(new NewsOptions { RefreshMinutes = 30 }),
            NullLogger<NewsService>.Instance);
    }

    private static NewsItem Item(string title, int daysAgo, string link = "https://example.org/x") =>
        new(title, link, "Testquelle", DateTimeOffset.UtcNow.AddDays(-daysAgo));

    [Fact]
    public async Task Sortiert_neueste_zuerst_und_begrenzt_auf_50()
    {
        NewsItem[] items = Enumerable.Range(0, 60).Reverse().Select(i => Item($"Meldung {i}", i)).ToArray();
        var reader = new FakeFeedReader().WithFeed(FeedA, items);
        NewsService service = CreateService(reader, FeedA);

        IReadOnlyList<NewsItem> result = await service.GetAsync(CancellationToken.None);

        Assert.Equal(50, result.Count);
        Assert.Equal("Meldung 0", result[0].Title);
    }

    [Fact]
    public async Task Ein_ausgefallener_Feed_laesst_die_anderen_durch()
    {
        var reader = new FakeFeedReader()
            .WithBrokenFeed(FeedA)
            .WithFeed(FeedB, Item("Aus Feed B", 1));
        NewsService service = CreateService(reader, FeedA, FeedB);

        IReadOnlyList<NewsItem> result = await service.GetAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Aus Feed B", result[0].Title);
    }

    [Fact]
    public async Task Alle_Feeds_ausgefallen_ergibt_leere_Liste_ohne_Exception()
    {
        var reader = new FakeFeedReader().WithBrokenFeed(FeedA).WithBrokenFeed(FeedB);
        NewsService service = CreateService(reader, FeedA, FeedB);

        IReadOnlyList<NewsItem> result = await service.GetAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Unsichere_Links_fliegen_raus_und_Titel_wird_bereinigt()
    {
        var reader = new FakeFeedReader().WithFeed(FeedA,
            Item("<b>Wichtig</b> &amp; dringend", 1),
            Item("Böse", 2, "javascript:alert(1)"));
        NewsService service = CreateService(reader, FeedA);

        IReadOnlyList<NewsItem> result = await service.GetAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Wichtig & dringend", result[0].Title);
    }
}
