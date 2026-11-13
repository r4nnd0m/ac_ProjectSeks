using System.ComponentModel.DataAnnotations;

namespace ScanApi.Models;

public record CreateProjectRequest([Required, StringLength(200)] string Name);

public record ProjectResponse(Guid Id, string Name, DateTimeOffset CreatedAt);
