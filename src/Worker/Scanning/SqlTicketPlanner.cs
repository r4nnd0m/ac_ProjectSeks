using Microsoft.EntityFrameworkCore;
using Sims.Shared.Data;
using Sims.Shared.Entities;
using Sims.Shared.Enums;
using Sims.Shared.Tickets;
using Worker.Enrichment;
using Worker.Osv;

namespace Worker.Scanning;

internal sealed record PreparedSqlScan(Guid ScanId, IReadOnlyList<Finding> Findings,
    IReadOnlyList<Ticket> Tickets) : IPreparedScanTickets
{
    public int Count => Tickets.Count;
}

internal sealed class SqlTicketPlanner(SimsDbContext db, TicketCreator creator) : IScanTicketPlanner
{
    public async Task<IPreparedScanTickets> PrepareAsync(PendingScan scan,
        IReadOnlyList<EnrichedFinding> findings, DateTimeOffset now, CancellationToken ct)
    {
        if (findings.Count == 0) return new PreparedSqlScan(scan.Id, [], []);

        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == scan.ProjectId, ct);
        var components = await db.Components.AsNoTracking().Where(c => c.ScanId == scan.Id)
            .ToDictionaryAsync(c => c.Id, ct);
        var entities = findings.Select(f => new Finding
        {
            ScanId = scan.Id,
            ComponentId = f.Finding.ComponentId,
            Component = components[f.Finding.ComponentId],
            VulnId = f.Finding.VulnId,
            Cve = f.Finding.Cve,
            Severity = f.Finding.Severity switch
            {
                OsvSeverity.Low => Severity.Low,
                OsvSeverity.Medium => Severity.Medium,
                OsvSeverity.High => Severity.High,
                OsvSeverity.Critical => Severity.Critical,
                _ => throw new InvalidOperationException("Unbekannter OSV-Schweregrad.")
            },
            Summary = f.Finding.Summary,
            IsKev = f.IsKev,
            Epss = f.Epss
        }).ToArray();

        var existing = await db.Tickets.AsNoTracking()
            .Where(t => t.ProjectId == project.Id && t.Status != TicketStatus.Closed)
            .ToListAsync(ct);
        return new PreparedSqlScan(scan.Id, entities, creator.CreateFor(project, entities, existing, now));
    }
}
