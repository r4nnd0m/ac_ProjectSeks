using Worker.Osv;

namespace Worker.Enrichment;

public interface IFindingEnricher
{
    Task<IReadOnlyList<EnrichedFinding>> EnrichAsync(IReadOnlyList<OsvFinding> findings, CancellationToken ct);
}
