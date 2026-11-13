namespace Worker.Scanning;

// Läuft, wenn keine Datenbank konfiguriert ist.
internal sealed class UnconfiguredScanProcessor(ILogger<UnconfiguredScanProcessor> logger) : IScanProcessor
{
    public Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Scan-Verarbeitung pausiert, ConnectionStrings:Sims ist nicht gesetzt.");
        return Task.CompletedTask;
    }
}

internal sealed class UnconfiguredScanRecovery(ILogger<UnconfiguredScanRecovery> logger) : IScanRecovery
{
    public Task RecoverAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        logger.LogInformation("Neustartprüfung pausiert, bis die Scan-Verarbeitung aktiviert ist.");
        return Task.CompletedTask;
    }
}
