namespace Worker.Escalation;

/// <summary>
/// Verbindet Laden, Regelaufruf, Ticketänderung und Audit.
/// Noch nicht im normalen Host angeschlossen: Die Adapter zu B1/C1/C4/C5 fehlen.
/// </summary>
public sealed class EscalationProcessor(
    IEscalationStore store,
    IEscalationPolicy policy,
    IEscalationAudit audit,
    TimeProvider clock,
    ILogger<EscalationProcessor> logger) : IEscalationProcessor
{
    public async Task RunAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var now = clock.GetUtcNow();
        var tickets = await store.GetDueTicketsAsync(now, ct);
        var changed = 0;
        foreach (var ticket in tickets)
        {
            ct.ThrowIfCancellationRequested();
            // Auch an der Worker-Grenze prüfen. Die spätere SQL-Abfrage filtert ebenso.
            if (ticket.IsFinished || ticket.DueAt is null || ticket.DueAt >= now || ticket.EscalationLevel >= 3)
                continue;

            var next = policy.NextLevel(ticket, now);
            if (next is null) continue;

            var from = ticket.EscalationLevel;
            ticket.Escalate(next.Level, next.AssignedRole, next.DueAt);
            audit.RecordEscalation(new EscalationAuditEntry(Guid.Empty, "SIMS Worker", ticket.Id, from, next.Level));
            changed++;
        }

        ct.ThrowIfCancellationRequested();
        await store.SaveChangesAsync(ct);
        logger.LogInformation("Eskalationsprüfung abgeschlossen: {Count} Ticketänderungen gemeinsam gespeichert.", changed);
    }
}
