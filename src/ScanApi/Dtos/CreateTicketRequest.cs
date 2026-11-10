using System.ComponentModel.DataAnnotations;
using Sims.Shared.Enums;

namespace ScanApi.Dtos;

public record CreateTicketRequest(
    [Required, StringLength(200)] string Title,
    [StringLength(4000)] string Description,
    Severity Severity,
    [RegularExpression(@"^CVE-\d{4}-\d{4,}$")] string? Cve,
    [StringLength(200)] string? System,
    Guid? ProjectId);