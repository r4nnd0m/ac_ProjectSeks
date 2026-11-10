using Sims.Contracts;
using Sims.Shared.Enums;

namespace Sims.Shared.Escalation;

public record EscalationStart(int Level, string? AssignedRole, DateTimeOffset? DueAt);

// PLATZHALTER, bis Rolle 6 die echte EscalationPolicy (C4) liefert.
// Die Fristen stehen hier nur vorläufig im Code, die echte Klasse liest sie aus den Options.
public class EscalationPolicy
{
    public EscalationStart StartFor(Severity severity, bool isKev, DateTimeOffset now)
    {
        if (isKev)
        {
            return new EscalationStart(3, Roles.Admin, null);
        }

        DateTimeOffset? due = severity switch
        {
            Severity.Critical => now.AddHours(4),
            Severity.High => now.AddHours(24),
            Severity.Medium => now.AddHours(72),
            _ => null
        };

        return new EscalationStart(1, null, due);
    }
}