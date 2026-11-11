using Worker.Enrichment;
using Worker.Osv;

namespace Worker.Scanning;

// Vorläufige Worker-Verträge; keine eigenen Shared-Entities oder Team-Regeln.
public sealed record PendingScan(Guid Id, Guid ProjectId, DateTimeOffset CreatedAt);
public enum ScanVerdict { Go, GoWithConditions, NoGo }

public interface IPreparedScanTickets
{
    // Der C3-Adapter hält hier noch ungespeicherte C1-Tickets. Kein Save im Planner.
    int Count { get; }
}

public sealed record ScanCompletion(
    IReadOnlyList<EnrichedFinding> Findings, ScanVerdict Verdict,
    IPreparedScanTickets Tickets, DateTimeOffset FinishedAt);

// Nur nach bestätigtem Rollback. Ein unklarer Commit-Ausgang ist kein solcher Fehler.
public sealed class ScanResultRejectedException(Exception cause)
    : Exception("SQL hat die Ergebnisdaten abgewiesen.", cause);

public interface IScanStore
{
    // Schon in SQL auf Pending filtern, nach CreatedAt/Id ordnen und begrenzen.
    Task<IReadOnlyList<PendingScan>> GetPendingAsync(int limit, CancellationToken ct);
    // Pending -> Scanning atomar sofort speichern; false, falls nicht mehr Pending.
    // Altes Urteil, ErrorMessage und FinishedAt entfernen.
    Task<bool> TryStartAsync(Guid scanId, CancellationToken ct);
    Task<IReadOnlyList<OsvComponent>> GetComponentsAsync(Guid scanId, CancellationToken ct);
    // Findings, neue Tickets, Verdict, FinishedAt und Scanned atomar speichern.
    // Nur Scanning abschließen; keine getrackten Altänderungen mitschreiben.
    Task CompleteAsync(Guid scanId, ScanCompletion completion, CancellationToken ct);
    // Eigener sauberer Schreibvorgang: Failed, FinishedAt, allgemeiner Text, Verdict=null.
    // Niemals vorbereitete Findings/Tickets aus einem Fehlversuch mitspeichern.
    Task FailAsync(Guid scanId, string message, DateTimeOffset finishedAt, CancellationToken ct);
    // Nur beim Start: Scanning -> Pending, Metadaten leeren. MVP: EINE Worker-Instanz.
    Task<int> ResetInterruptedAsync(CancellationToken ct);
}

public interface IScanVerdictCalculator
{
    // B4 aufrufen; Enums später nach Namen abbilden, nicht numerisch casten.
    ScanVerdict Calculate(IReadOnlyList<EnrichedFinding> findings);
}

public interface IScanTicketPlanner
{
    // Projekt + vorhandene Tickets laden, dann C3 (inkl. C1/C4-Regeln) aufrufen.
    // Deduplizierung ist C3-Aufgabe. Neue Tickets nur vorbereiten, nicht speichern.
    Task<IPreparedScanTickets> PrepareAsync(
        PendingScan scan, IReadOnlyList<EnrichedFinding> findings, DateTimeOffset now, CancellationToken ct);
}

public interface IScanRecovery
{
    Task RecoverAsync(CancellationToken ct);
}
