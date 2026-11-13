using Microsoft.EntityFrameworkCore;
using ScanApi.Dtos;
using ScanApi.Extensions;
using Sims.Shared.Data;

namespace ScanApi.Services;

public interface IAuditQueryService
{
    Task<PagedResponse<AuditEntryResponse>> ListAsync(AuditFilter filter, CancellationToken ct = default);
}

public class AuditQueryService(SimsDbContext db) : IAuditQueryService
{
    public async Task<PagedResponse<AuditEntryResponse>> ListAsync(AuditFilter filter, CancellationToken ct = default)
    {
        return await db.AuditEntries.AsNoTracking()
            .OrderByDescending(e => e.Timestamp).ThenBy(e => e.Id)
            .Select(e => new AuditEntryResponse(e.Id, e.Timestamp, e.UserName, e.Action, e.TicketId, e.Details))
            .ReadPageAsync(filter.Page, filter.PageSize, ct);
    }
}