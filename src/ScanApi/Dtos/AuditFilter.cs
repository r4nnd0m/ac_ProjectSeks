using System.ComponentModel.DataAnnotations;

namespace ScanApi.Dtos;

public record AuditFilter(
    [property: Range(1, int.MaxValue)] int Page = 1,
    [property: Range(1, 100)] int PageSize = 20);