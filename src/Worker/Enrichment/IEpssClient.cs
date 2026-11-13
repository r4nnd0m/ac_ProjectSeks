namespace Worker.Enrichment;

public interface IEpssClient
{
    // CVEs ohne EPSS-Wert fehlen im Ergebnis (nicht 0).
    Task<Dictionary<string, double>> GetScoresAsync(IReadOnlyList<string> cves, CancellationToken ct);
}
