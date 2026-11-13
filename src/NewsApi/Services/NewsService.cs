using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NewsApi.Models;
using NewsApi.Options;

namespace NewsApi.Services;

public class NewsService
{
    private const string CacheKey = "news";
    private const string LastGoodKey = "news-last-good";
    private const int MaxItems = 50;

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly IFeedReader _reader;
    private readonly IMemoryCache _cache;
    private readonly FeedOptions _feeds;
    private readonly NewsOptions _news;
    private readonly ILogger<NewsService> _logger;

    public NewsService(
        IFeedReader reader,
        IMemoryCache cache,
        IOptions<FeedOptions> feeds,
        IOptions<NewsOptions> news,
        ILogger<NewsService> logger)
    {
        _reader = reader;
        _cache = cache;
        _feeds = feeds.Value;
        _news = news.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NewsItem>> GetAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<NewsItem>? cached) && cached != null)
        {
            return cached;
        }

        await Gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(CacheKey, out cached) && cached != null)
            {
                return cached;
            }

            IReadOnlyList<NewsItem> fresh = await LoadFeedsAsync(ct);

            if (fresh.Count > 0)
            {
                _cache.Set(LastGoodKey, fresh);
            }
            else if (_cache.TryGetValue(LastGoodKey, out IReadOnlyList<NewsItem>? lastGood) && lastGood != null)
            {
                fresh = lastGood;
            }

            _cache.Set(CacheKey, fresh, TimeSpan.FromMinutes(_news.RefreshMinutes));
            return fresh;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<IReadOnlyList<NewsItem>> LoadFeedsAsync(CancellationToken ct)
    {
        var all = new List<NewsItem>();

        foreach (string url in _feeds.ActiveUrls)
        {
            try
            {
                IReadOnlyList<NewsItem> items = await _reader.ReadAsync(url, ct);
                all.AddRange(items);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Feed {Host} konnte nicht geladen werden ({Fehler}).", new Uri(url).Host, ex.GetType().Name);
            }
        }

        return all
            .Where(n => TextSanitizer.IsSafeLink(n.Link))
            .Select(n => n with
            {
                Title = TextSanitizer.Clean(n.Title),
                Source = TextSanitizer.Clean(n.Source, 100)
            })
            .OrderByDescending(n => n.PublishedAt)
            .Take(MaxItems)
            .ToList();
    }
}