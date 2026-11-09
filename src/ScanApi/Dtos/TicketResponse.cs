using Sims.Shared.Enums;

namespace ScanApi.Dtos;

public record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    Severity Severity,
    TicketStatus Status,
    string? Cve,
    string? System,
    Guid? ProjectId,
    string ReporterName,
    string? AssigneeName,
    string? AssignedRole,
    int EscalationLevel,
    DateTimeOffset? DueAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt);