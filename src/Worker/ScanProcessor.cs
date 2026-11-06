namespace Worker;

/// <summary>
/// Einstiegspunkt für die spätere Scan-Verarbeitung aus D3.
/// Solange das gemeinsame Datenmodell fehlt, findet hier noch kein Scan statt.
/// </summary>
public sealed class ScanProcessor(ILogger<ScanProcessor> logger) : IScanProcessor
{
    public Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation(
            "ScanProcessor aufgerufen. Platzhalter aktiv: " +
            "Noch keine Scan-Aufträge oder Datenbank angebunden.");

        return Task.CompletedTask;
    }
}
