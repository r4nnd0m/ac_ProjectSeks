namespace Worker.Enrichment;

public interface IKevClient
{
    /// <summary>Bekannt ausgenutzte CVEs, unabhängig von Groß-/Kleinschreibung vergleichbar.</summary>
    Task<HashSet<string>> GetKnownExploitedAsync(CancellationToken ct);
}
