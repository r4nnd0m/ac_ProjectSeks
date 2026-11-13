namespace NewsApi.Models;

public record NewsItem(string Title, string Link, string Source, DateTimeOffset PublishedAt);
