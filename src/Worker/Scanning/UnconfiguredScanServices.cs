namespace Worker.Scanning;

// Normaler Host bleibt ausdrücklich ohne SQL, bis die echten Adapter eingebunden sind.
internal sealed class UnconfiguredScanProcessor(ILogger<UnconfiguredScanProcessor> logger) : IScanProcessor
{
    public Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Scan-Platzhalter aktiv: Team-Adapter und Datenbank noch nicht angeschlossen. Keine Scan-Aufträge verarbeitet.");
        return Task.CompletedTask;
    }
}

internal sealed class UnconfiguredScanRecovery(ILogger<UnconfiguredScanRecovery> logger) : IScanRecovery
{
    public Task RecoverAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        logger.LogInformation("Neustart-Platzhalter aktiv: Ohne Datenbankanschluss wurden keine Scans zurückgesetzt.");
        return Task.CompletedTask;
    }
}
