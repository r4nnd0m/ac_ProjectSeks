using Sims.Shared.Entities;
using Sims.Shared.Enums;
using Worker.Osv;

namespace Worker.Enrichment;

public sealed record EnrichedFinding(OsvFinding Finding, bool IsKev, double? Epss)
{
    internal Finding ToSharedFinding() => new()
    {
        ComponentId = Finding.ComponentId,
        VulnId = Finding.VulnId,
        Cve = Finding.Cve,
        Severity = Finding.Severity switch
        {
            OsvSeverity.Low => Severity.Low,
            OsvSeverity.Medium => Severity.Medium,
            OsvSeverity.High => Severity.High,
            OsvSeverity.Critical => Severity.Critical,
            _ => throw new InvalidOperationException("Unbekannter OSV-Schweregrad.")
        },
        Summary = Finding.Summary,
        IsKev = IsKev,
        Epss = Epss
    };
}
