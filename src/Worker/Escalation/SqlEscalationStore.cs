using System.Data;
using Microsoft.EntityFrameworkCore;
using Sims.Shared.Audit;
using Sims.Shared.Data;
using Sims.Shared.Entities;
using Sims.Shared.Enums;

namespace Worker.Escalation;

internal sealed class SqlEscalationTicket(Ticket ticket) : IEscalationTicket
{
    public Ticket Entity => ticket;
    public Guid Id => ticket.Id;
    public bool IsFinished => ticket.IsFinished;
    public int EscalationLevel => ticket.EscalationLevel;
    public DateTimeOffset? DueAt => ticket.DueAt;
    public void Escalate(int newLevel, string assignedRole, DateTimeOffset? newDueAt) =>
        ticket.Escalate(newLevel, assignedRole, newDueAt);
}

internal sealed class SqlEscalationStore(SimsDbContext db) : IEscalationStore
{
    public async Task<IReadOnlyList<IEscalationTicket>> GetDueTicketsAsync(DateTimeOffset now, CancellationToken ct)
    {
        // RepeatableRead, damit ein parallel geschlossenes Ticket nicht trotzdem eskaliert wird.
        await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var tickets = await db.Tickets.Where(t => t.Status != TicketStatus.Closed
                && t.Status != TicketStatus.RiskAccepted && t.EscalationLevel < 3 && t.DueAt < now)
            .OrderBy(t => t.Id).ToListAsync(ct);
        return tickets.Select(t => new SqlEscalationTicket(t)).ToArray();
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await db.Database.CommitTransactionAsync(ct);
    }
}

internal sealed class SqlEscalationAudit(AuditService audit) : IEscalationAudit
{
    public void RecordEscalation(EscalationAuditEntry entry) =>
        audit.Record(entry.UserId, entry.UserName, AuditActions.TicketEscalated, entry.TicketId, entry.Details);
}
