namespace Worker.Osv;

public interface IOsvClient
{
    // Wirft OsvUnavailableException, wenn kein vollständiges Ergebnis vorliegt.
    Task<IReadOnlyList<OsvFinding>> FindVulnerabilitiesAsync(
        IReadOnlyList<OsvComponent> components, CancellationToken ct);
}
