using Worker.Enrichment;
using Worker.Osv;
using Worker.Scanning;

namespace Worker;

public sealed class ScanProcessor(
    IScanStore store, IOsvClient osv, IFindingEnricher enricher,
    IScanVerdictCalculator verdicts, IScanTicketPlanner tickets,
    TimeProvider clock, ILogger<ScanProcessor> logger) : IScanProcessor
{
    public const int BatchSize = 5;
    public const string FailureMessage = "Der Scan konnte nicht abgeschlossen werden.";

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pending = await store.GetPendingAsync(BatchSize, cancellationToken);
        foreach (var scan in pending.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id)
                     .DistinctBy(s => s.Id).Take(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await store.TryStartAsync(scan.Id, cancellationToken)) continue;
            await ProcessScanAsync(scan, cancellationToken);
        }
    }

    private async Task ProcessScanAsync(PendingScan scan, CancellationToken ct)
    {
        logger.LogInformation("Scan {ScanId}: Scanning gespeichert; externe Prüfung beginnt.", scan.Id);
        ScanCompletion completion;
        try
        {
            completion = await PrepareResultAsync(scan, ct);
        }
        catch (Exception ex)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogError("Scan {ScanId}: Verarbeitung fehlgeschlagen ({ErrorType}).", scan.Id, ex.GetType().Name);
            await store.FailAsync(scan.Id, FailureMessage, clock.GetUtcNow(), ct);
            return;
        }

        // Nur bei einem sicheren Rollback auf Failed setzen. Bei anderen Fehlern ist unklar,
        // ob der Commit durchging, dann übernimmt die Recovery beim Neustart.
        ct.ThrowIfCancellationRequested();
        try
        {
            await store.CompleteAsync(scan.Id, completion, ct);
        }
        catch (ScanResultRejectedException)
        {
            logger.LogError("Scan {ScanId}: SQL hat die Ergebnisdaten abgewiesen. Die Änderungen wurden zurückgerollt.", scan.Id);
            await store.FailAsync(scan.Id, FailureMessage, clock.GetUtcNow(), ct);
            return;
        }
        logger.LogInformation("Scan {ScanId}: Scanned gespeichert; {Findings} Funde, {Tickets} neue Tickets, Urteil {Verdict}.",
            scan.Id, completion.Findings.Count, completion.Tickets.Count, completion.Verdict);
    }

    private async Task<ScanCompletion> PrepareResultAsync(PendingScan scan, CancellationToken ct)
    {
        var components = await store.GetComponentsAsync(scan.Id, ct);
        var findings = await osv.FindVulnerabilitiesAsync(components, ct);
        var enriched = await enricher.EnrichAsync(findings, ct);
        ct.ThrowIfCancellationRequested();
        var verdict = verdicts.Calculate(enriched);
        if (!Enum.IsDefined(verdict)) throw new InvalidOperationException("Ungültiges Urteil vom Adapter.");
        var prepared = await tickets.PrepareAsync(scan, enriched, clock.GetUtcNow(), ct);
        ct.ThrowIfCancellationRequested();
        return new ScanCompletion(enriched, verdict, prepared, clock.GetUtcNow());
    }
}
