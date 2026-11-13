namespace Worker.Escalation;

public interface IEscalationTicket
{
    Guid Id { get; }
    bool IsFinished { get; }
    int EscalationLevel { get; }
    DateTimeOffset? DueAt { get; }
    void Escalate(int newLevel, string assignedRole, DateTimeOffset? newDueAt);
}

public sealed record EscalationStep(int Level, string AssignedRole, DateTimeOffset? DueAt);

public interface IEscalationPolicy
{
    // null = Ticket bleibt auf der aktuellen Stufe
    EscalationStep? NextLevel(IEscalationTicket ticket, DateTimeOffset now);
}

public interface IEscalationStore
{
    Task<IReadOnlyList<IEscalationTicket>> GetDueTicketsAsync(DateTimeOffset now, CancellationToken ct);

    // Speichert Ticketänderungen und Audit-Einträge gemeinsam in einer Transaktion.
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record EscalationAuditEntry(
    Guid UserId, string UserName, Guid TicketId, int FromLevel, int ToLevel)
{
    public string Details => $"Stufe {FromLevel} auf {ToLevel}";
}

public interface IEscalationAudit
{
    // Merkt den Eintrag nur vor, gespeichert wird über IEscalationStore.SaveChangesAsync.
    void RecordEscalation(EscalationAuditEntry entry);
}
