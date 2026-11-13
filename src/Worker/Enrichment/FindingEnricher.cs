using Worker.Osv;

namespace Worker.Enrichment;

public sealed class FindingEnricher(IKevClient kev, IEpssClient epss) : IFindingEnricher
{
    public async Task<IReadOnlyList<EnrichedFinding>> EnrichAsync(IReadOnlyList<OsvFinding> findings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ct.ThrowIfCancellationRequested();
        var knownExploited = await kev.GetKnownExploitedAsync(ct);
        var cves = findings.Where(f => f.Cve is not null).Select(f => f.Cve!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var scores = await epss.GetScoresAsync(cves, ct);
        ct.ThrowIfCancellationRequested();
        return findings.Select(f => new EnrichedFinding(f,
            f.Cve is not null && knownExploited.Contains(f.Cve),
            f.Cve is not null && scores.TryGetValue(f.Cve, out var score) ? score : null)).ToArray();
    }
}
