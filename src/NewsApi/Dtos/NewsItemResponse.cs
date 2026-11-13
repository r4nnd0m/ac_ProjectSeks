namespace NewsApi.Dtos;

public record NewsItemResponse(string Title, string Link, string Source, DateTimeOffset PublishedAt);
