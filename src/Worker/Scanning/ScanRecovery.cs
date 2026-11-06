namespace Worker.Scanning;

public sealed class ScanRecovery(IScanStore store, ILogger<ScanRecovery> logger) : IScanRecovery
{
    public async Task RecoverAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var count = await store.ResetInterruptedAsync(ct);
        logger.LogInformation("Neustartprüfung abgeschlossen: {Count} unterbrochene Scans wieder auf Pending gesetzt.", count);
    }
}
