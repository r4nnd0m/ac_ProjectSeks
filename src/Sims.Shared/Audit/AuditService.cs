using Sims.Shared.Data;
using Sims.Shared.Entities;

namespace Sims.Shared.Audit;

public class AuditService(SimsDbContext db, TimeProvider clock)
{
    public void Record(Guid userId, string userName, string action, Guid? ticketId, string? details)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            Timestamp = clock.GetUtcNow(),
            UserId = userId,
            UserName = userName,
            Action = action,
            TicketId = ticketId,
            Details = details
        });
    }
}