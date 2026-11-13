using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Sims.Shared.Data;
using Sims.Shared.Enums;
using Worker.Osv;

namespace Worker.Scanning;

internal sealed class SqlScanStore(SimsDbContext db) : IScanStore
{
    public async Task<IReadOnlyList<PendingScan>> GetPendingAsync(int limit, CancellationToken ct) =>
        await db.Scans.Where(s => s.Status == ScanStatus.Pending)
            .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).Take(limit)
            .Select(s => new PendingScan(s.Id, s.ProjectId, s.CreatedAt)).ToListAsync(ct);

    public async Task<bool> TryStartAsync(Guid scanId, CancellationToken ct) =>
        await db.Scans.Where(s => s.Id == scanId && s.Status == ScanStatus.Pending)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, ScanStatus.Scanning)
                .SetProperty(s => s.FinishedAt, (DateTimeOffset?)null)
                .SetProperty(s => s.ErrorMessage, (string?)null)
                .SetProperty(s => s.Verdict, (Verdict?)null), ct) == 1;

    public async Task<IReadOnlyList<OsvComponent>> GetComponentsAsync(Guid scanId, CancellationToken ct) =>
        await db.Components.Where(c => c.ScanId == scanId).OrderBy(c => c.Id)
            .Select(c => new OsvComponent(c.Id, c.Purl)).ToListAsync(ct);

    public async Task CompleteAsync(Guid scanId, ScanCompletion completion, CancellationToken ct)
    {
        if (completion.Tickets is not PreparedSqlScan result || result.ScanId != scanId)
            throw new InvalidOperationException("Die vorbereiteten Ergebnisse gehören nicht zu diesem Scan.");

        var verdict = completion.Verdict switch
        {
            ScanVerdict.Go => Verdict.Go,
            ScanVerdict.GoWithConditions => Verdict.GoWithConditions,
            ScanVerdict.NoGo => Verdict.NoGo,
            _ => throw new InvalidOperationException("Ungültiges Scanurteil.")
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Scans.Where(s => s.Id == scanId && s.Status == ScanStatus.Scanning)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, ScanStatus.Scanned)
                .SetProperty(s => s.Verdict, verdict)
                .SetProperty(s => s.FinishedAt, completion.FinishedAt), ct);
        if (changed != 1) throw new DbUpdateConcurrencyException("Scan ist nicht mehr in Bearbeitung.");

        try
        {
            // Component wird nur für Ticket-Titel und Duplikatprüfung gebraucht und existiert schon in der DB.
            foreach (var finding in result.Findings) db.Entry(finding).State = EntityState.Added;
            db.Tickets.AddRange(result.Tickets);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 515 or 547 or 2601 or 2627 or 2628 or 8152 })
        {
            // NOT NULL, Fremdschlüssel, Unique oder Feldlänge: ein neuer Versuch würde genauso scheitern.
            await tx.RollbackAsync(ct);
            throw new ScanResultRejectedException(ex);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
        await tx.CommitAsync(ct);
    }

    public async Task FailAsync(Guid scanId, string message, DateTimeOffset finishedAt, CancellationToken ct)
    {
        var changed = await db.Scans.Where(s => s.Id == scanId && s.Status == ScanStatus.Scanning)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, ScanStatus.Failed)
                .SetProperty(s => s.Verdict, (Verdict?)null)
                .SetProperty(s => s.ErrorMessage, message).SetProperty(s => s.FinishedAt, finishedAt), ct);
        if (changed != 1) throw new DbUpdateConcurrencyException("Scan ist nicht mehr in Bearbeitung.");
    }

    public Task<int> ResetInterruptedAsync(CancellationToken ct) =>
        db.Scans.Where(s => s.Status == ScanStatus.Scanning)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, ScanStatus.Pending)
                .SetProperty(s => s.Verdict, (Verdict?)null).SetProperty(s => s.ErrorMessage, (string?)null)
                .SetProperty(s => s.FinishedAt, (DateTimeOffset?)null), ct);
}
