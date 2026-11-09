using Sims.Shared.Enums;

namespace Sims.Shared.Entities;

public record NewTicket(
    Guid TeamId,
    string Title,
    string Description,
    Severity Severity,
    Guid ReporterId,
    string ReporterName,
    Guid? ProjectId = null,
    Guid? FindingId = null,
    string? Cve = null,
    string? System = null,
    int EscalationLevel = 1,
    string? AssignedRole = null,
    DateTimeOffset? DueAt = null);