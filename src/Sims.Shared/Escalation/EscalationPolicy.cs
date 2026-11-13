using Microsoft.Extensions.Options;
using Sims.Contracts;
using Sims.Shared.Entities;
using Sims.Shared.Enums;

namespace Sims.Shared.Escalation;

public record EscalationStart(int Level, string? AssignedRole, DateTimeOffset? DueAt);
public record EscalationStep(int Level, string AssignedRole, DateTimeOffset? DueAt);

public sealed class EscalationOptions
{
    public double CriticalHours { get; set; } = 4;
    public double HighHours { get; set; } = 24;
    public double MediumHours { get; set; } = 72;
}

public class EscalationPolicy(IOptions<EscalationOptions> options)
{
    public EscalationPolicy() : this(Options.Create(new EscalationOptions())) { }

    public EscalationStart StartFor(Severity severity, bool isKev, DateTimeOffset now) =>
        isKev ? new(3, Roles.Admin, null) : new(1, null, DueFor(severity, now));

    public EscalationStep? NextLevel(Ticket ticket, DateTimeOffset now)
    {
        if (ticket.IsFinished || ticket.DueAt is null || ticket.DueAt >= now) return null;

        return ticket.EscalationLevel switch
        {
            1 => new(2, Roles.Lead, DueFor(ticket.Severity, now)),
            2 => new(3, Roles.Admin, null),
            _ => null
        };
    }

    private DateTimeOffset? DueFor(Severity severity, DateTimeOffset now) => severity switch
    {
        Severity.Critical => now.AddHours(options.Value.CriticalHours),
        Severity.High => now.AddHours(options.Value.HighHours),
        Severity.Medium => now.AddHours(options.Value.MediumHours),
        Severity.Low => null,
        _ => throw new ArgumentOutOfRangeException(nameof(severity))
    };
}
