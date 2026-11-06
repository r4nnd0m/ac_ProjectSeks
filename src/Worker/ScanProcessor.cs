using Worker.Enrichment;
using Worker.Osv;
using Worker.Scanning;

namespace Worker;

/// <summary>Vorbereiteter D3-Ablauf. Die echten B1/B4/C3-Adapter fehlen noch.</summary>
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
        // Die SQL-Abfrage muss ebenfalls sortieren/begrenzen. Hier zusätzlich absichern.
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
                // Scanning bleibt stehen; die Start-Recovery übernimmt beim Neustart.
                throw;
            }
            catch (Exception exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogError("Scan {ScanId}: Verarbeitung fehlgeschlagen ({ErrorType}).", scan.Id, exception.GetType().Name);
                await store.FailAsync(scan.Id, FailureMessage, clock.GetUtcNow(), cancellationToken);
                continue;
            }

            // Bei unklarem Commit-Ausgang NICHT blind Failed schreiben oder Tickets
            // erneut erzeugen. Der SQL-Adapter muss atomar speichern. Fehler gehen an
            // den Job; ein verbliebenes Scanning wird beim Neustart erkannt.
            cancellationToken.ThrowIfCancellationRequested();
            await store.CompleteAsync(scan.Id, completion, cancellationToken);
            logger.LogInformation("Scan {ScanId}: Scanned gespeichert; {Findings} Funde, {Tickets} neue Tickets, Urteil {Verdict}.",
                scan.Id, completion.Findings.Count, completion.Tickets.Count, completion.Verdict);
        }
    }
}
