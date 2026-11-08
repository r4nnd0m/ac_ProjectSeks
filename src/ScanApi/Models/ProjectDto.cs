namespace ScanApi.Models;

public record CreateProjectRequest(string Name);

public record ProjectResponse(Guid Id, string Name, DateTimeOffset CreatedAt);
