namespace Sims.Shared.Entities;

public class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Guid? TicketId { get; set; }
    public string? Details { get; set; }
}