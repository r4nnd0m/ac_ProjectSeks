using Sims.Shared.Enums;
using Sims.Shared.Escalation;
using Sims.Shared.Rules;
using Worker.Enrichment;
using Worker.Escalation;

namespace Worker.Scanning;

internal sealed class ScanVerdictAdapter(VerdictCalculator calculator) : IScanVerdictCalculator
{
    public ScanVerdict Calculate(IReadOnlyList<EnrichedFinding> findings) =>
        calculator.Calculate(findings.Select(f => f.ToSharedFinding()).ToArray()) switch
        {
            Verdict.Go => ScanVerdict.Go,
            Verdict.GoWithConditions => ScanVerdict.GoWithConditions,
            Verdict.NoGo => ScanVerdict.NoGo,
            _ => throw new InvalidOperationException("Ungültiges Scanurteil.")
        };
}

internal sealed class EscalationPolicyAdapter(EscalationPolicy policy) : IEscalationPolicy
{
    public Escalation.EscalationStep? NextLevel(IEscalationTicket ticket, DateTimeOffset now)
    {
        var step = policy.NextLevel(((SqlEscalationTicket)ticket).Entity, now);
        return step is null ? null : new(step.Level, step.AssignedRole, step.DueAt);
    }
}
