namespace Worker.Enrichment;

public interface IEpssClient
{
    /// <summary>Vorhandene EPSS-Werte. Fehlende CVEs haben keinen Eintrag, niemals automatisch 0.</summary>
    Task<Dictionary<string, double>> GetScoresAsync(IReadOnlyList<string> cves, CancellationToken ct);
}
