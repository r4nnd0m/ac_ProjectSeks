namespace Worker.Osv;

public interface IOsvClient
{
    /// <summary>
    /// Liefert zugeordnete Funde; bei unvollständiger/fehlgeschlagener Abfrage
    /// OsvUnavailableException. Ein angeforderter Abbruch bleibt ein Abbruch.
    /// </summary>
    Task<IReadOnlyList<OsvFinding>> FindVulnerabilitiesAsync(
        IReadOnlyList<OsvComponent> components, CancellationToken ct);
}
