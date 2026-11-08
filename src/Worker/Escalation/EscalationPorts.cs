namespace Worker.Escalation;

// Vorläufige Worker-Anschlüsse, solange B1/C1/C4/C5 noch fehlen.
// Adapter sollen später die gemeinsamen Klassen aufrufen, nicht deren Regeln kopieren.
public interface IEscalationTicket
{
    Guid Id { get; }
    bool IsFinished { get; } // C1: Closed oder RiskAccepted.
    int EscalationLevel { get; }
    DateTimeOffset? DueAt { get; }
    void Escalate(int newLevel, string assignedRole, DateTimeOffset? newDueAt);
}

public sealed record EscalationStep(int Level, string AssignedRole, DateTimeOffset? DueAt);

public interface IEscalationPolicy
{
    // C4 bestimmt Rolle und neue Frist. null bedeutet: keine Änderung.
    EscalationStep? NextLevel(IEscalationTicket ticket, DateTimeOffset now);
}

public interface IEscalationStore
{
    // Später: eine gefilterte EF-Abfrage und Wrapper um dieselben getrackten C1-Tickets.
    Task<IReadOnlyList<IEscalationTicket>> GetDueTicketsAsync(DateTimeOffset now, CancellationToken ct);

    // Ticketänderungen UND vorgemerkte Audit-Einträge in EINEM DbContext atomar speichern.
    // Keine Änderungen bereits in GetDueTicketsAsync, Escalate oder RecordEscalation speichern.
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record EscalationAuditEntry(
    Guid UserId, string UserName, Guid TicketId, int FromLevel, int ToLevel)
{
    public string Details => $"Stufe {FromLevel} auf {ToLevel}";
}

public interface IEscalationAudit
{
    // C5-Adapter: AuditService.Record(..., AuditActions.TicketEscalated, ...).
    // Nur vormerken, nicht selbst speichern. Derselbe DbContext wie IEscalationStore.
    void RecordEscalation(EscalationAuditEntry entry);
}
