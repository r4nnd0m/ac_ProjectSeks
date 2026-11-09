using System.ComponentModel.DataAnnotations;
using Sims.Shared.Enums;

namespace ScanApi.Dtos;

public record CreateTicketRequest(
    [property: Required, StringLength(200)] string Title,
    [property: StringLength(4000)] string Description,
    Severity Severity,
    [property: RegularExpression(@"^CVE-\d{4}-\d{4,}$")] string? Cve,
    [property: StringLength(200)] string? System,
    Guid? ProjectId);