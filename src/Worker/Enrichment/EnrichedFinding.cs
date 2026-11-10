using Worker.Osv;

namespace Worker.Enrichment;

/// <summary>Vorbereitetes Ergebnis im Arbeitsspeicher, noch keine B1-Entity.</summary>
public sealed record EnrichedFinding(OsvFinding Finding, bool IsKev, double? Epss);
