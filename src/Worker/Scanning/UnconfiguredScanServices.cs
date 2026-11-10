namespace Worker.Scanning;

// Ohne die gemeinsame Datenbank und die Teamregeln bleiben Aufträge unangetastet.
internal sealed class UnconfiguredScanProcessor(ILogger<UnconfiguredScanProcessor> logger) : IScanProcessor
{
    public Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Scan-Verarbeitung pausiert. Sie benötigt ConnectionStrings:Sims, das Scanurteil (B4) und die Startregel für Tickets (C4).");
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
