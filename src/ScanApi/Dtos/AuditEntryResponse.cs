namespace ScanApi.Dtos;

public record AuditEntryResponse(
    Guid Id,
    DateTimeOffset Timestamp,
    string UserName,
    string Action,
    Guid? TicketId,
    string? Details);