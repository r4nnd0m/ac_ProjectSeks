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
            logger.LogInformation("Scan {ScanId}: Scanning gespeichert; externe Prüfung beginnt.", scan.Id);

            ScanCompletion completion;
            try
            {
                var components = await store.GetComponentsAsync(scan.Id, cancellationToken);
                var findings = await osv.FindVulnerabilitiesAsync(components, cancellationToken);
                var enriched = await enricher.EnrichAsync(findings, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var verdict = verdicts.Calculate(enriched);
                if (!Enum.IsDefined(verdict)) throw new InvalidOperationException("Ungültiges Urteil vom Adapter.");
                var prepared = await tickets.PrepareAsync(scan, enriched, clock.GetUtcNow(), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                completion = new ScanCompletion(enriched, verdict, prepared, clock.GetUtcNow());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Scan bleibt auf Scanning und wird beim nächsten Start zurückgesetzt.
                throw;
            }
            catch (Exception exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogError("Scan {ScanId}: Verarbeitung fehlgeschlagen ({ErrorType}).", scan.Id, exception.GetType().Name);
                await store.FailAsync(scan.Id, FailureMessage, clock.GetUtcNow(), cancellationToken);
                continue;
            }

            // Nur bei einem sicheren Rollback auf Failed setzen. Bei anderen Fehlern ist unklar,
            // ob der Commit durchging, dann übernimmt die Recovery beim Neustart.
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await store.CompleteAsync(scan.Id, completion, cancellationToken);
            }
            catch (ScanResultRejectedException)
            {
                logger.LogError("Scan {ScanId}: SQL hat die Ergebnisdaten abgewiesen. Die Änderungen wurden zurückgerollt.", scan.Id);
                await store.FailAsync(scan.Id, FailureMessage, clock.GetUtcNow(), cancellationToken);
                continue;
            }
            logger.LogInformation("Scan {ScanId}: Scanned gespeichert; {Findings} Funde, {Tickets} neue Tickets, Urteil {Verdict}.",
                scan.Id, completion.Findings.Count, completion.Tickets.Count, completion.Verdict);
        }
    }
}
