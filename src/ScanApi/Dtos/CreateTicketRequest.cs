using System.ComponentModel.DataAnnotations;
using Sims.Shared.Entities;
using Sims.Shared.Enums;

namespace ScanApi.Dtos;

public record CreateTicketRequest(
    [Required, StringLength(Ticket.MaxTitleLength)] string Title,
    [StringLength(4000)] string Description,
    [EnumDataType(typeof(Severity))] Severity Severity,
    [StringLength(50), RegularExpression(@"^CVE-\d{4}-\d{4,}$")] string? Cve,
    [StringLength(200)] string? System,
    Guid? ProjectId);