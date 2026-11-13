using Worker.Enrichment;
using Worker.Osv;

namespace Worker.Scanning;

public sealed record PendingScan(Guid Id, Guid ProjectId, DateTimeOffset CreatedAt);
public enum ScanVerdict { Go, GoWithConditions, NoGo }

public interface IPreparedScanTickets
{
    int Count { get; }
}

public sealed record ScanCompletion(
    IReadOnlyList<EnrichedFinding> Findings, ScanVerdict Verdict,
    IPreparedScanTickets Tickets, DateTimeOffset FinishedAt);

// Nur werfen, wenn die Transaktion sicher zurückgerollt wurde.
public sealed class ScanResultRejectedException(Exception cause)
    : Exception("SQL hat die Ergebnisdaten abgewiesen.", cause);

public interface IScanStore
{
    Task<IReadOnlyList<PendingScan>> GetPendingAsync(int limit, CancellationToken ct);

    // Pending -> Scanning, false wenn der Scan nicht mehr Pending ist.
    Task<bool> TryStartAsync(Guid scanId, CancellationToken ct);

    Task<IReadOnlyList<OsvComponent>> GetComponentsAsync(Guid scanId, CancellationToken ct);

    // Speichert Funde, neue Tickets und Urteil in einer Transaktion.
    Task CompleteAsync(Guid scanId, ScanCompletion completion, CancellationToken ct);

    Task FailAsync(Guid scanId, string message, DateTimeOffset finishedAt, CancellationToken ct);

    // Setzt beim Start hängengebliebene Scans zurück. Geht von nur einer Worker-Instanz aus.
    Task<int> ResetInterruptedAsync(CancellationToken ct);
}

public interface IScanVerdictCalculator
{
    ScanVerdict Calculate(IReadOnlyList<EnrichedFinding> findings);
}

public interface IScanTicketPlanner
{
    // Bereitet neue Tickets nur vor, gespeichert wird in IScanStore.CompleteAsync.
    Task<IPreparedScanTickets> PrepareAsync(
        PendingScan scan, IReadOnlyList<EnrichedFinding> findings, DateTimeOffset now, CancellationToken ct);
}

public interface IScanRecovery
{
    Task RecoverAsync(CancellationToken ct);
}
