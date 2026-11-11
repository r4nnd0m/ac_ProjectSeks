using NewsApi.Models;

namespace NewsApi.Services;

public interface IFeedReader
{
    Task<IReadOnlyList<NewsItem>> ReadAsync(string url, CancellationToken ct);
}
