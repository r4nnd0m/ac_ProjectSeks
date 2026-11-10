using Microsoft.EntityFrameworkCore;
using ScanApi.Dtos;
using Sims.Shared.Data;
using Sims.Shared.Entities;

namespace ScanApi.Services;

public interface IAuditQueryService
{
    Task<PagedResponse<AuditEntryResponse>> ListAsync(AuditFilter filter, CancellationToken ct = default);
}

public class AuditQueryService(SimsDbContext db) : IAuditQueryService
{
    public async Task<PagedResponse<AuditEntryResponse>> ListAsync(AuditFilter filter, CancellationToken ct = default)
    {
        IQueryable<AuditEntry> query = db.AuditEntries.AsNoTracking();

        int total = await query.CountAsync(ct);
        List<AuditEntryResponse> items = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(e => new AuditEntryResponse(e.Id, e.Timestamp, e.UserName, e.Action, e.TicketId, e.Details))
            .ToListAsync(ct);

        return new PagedResponse<AuditEntryResponse>(items, filter.Page, filter.PageSize, total);
    }
}