using System.ComponentModel.DataAnnotations;

namespace ScanApi.Dtos;

public record AuditFilter(
    [Range(1, int.MaxValue)] int Page = 1,
    [Range(1, 100)] int PageSize = 20);