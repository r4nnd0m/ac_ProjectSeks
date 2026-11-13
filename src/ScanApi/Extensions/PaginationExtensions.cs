using Microsoft.EntityFrameworkCore;
using ScanApi.Dtos;

namespace ScanApi.Extensions;

internal static class PaginationExtensions
{
    internal static async Task<PagedResponse<T>> ReadPageAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        int total = await query.CountAsync(ct);
        long offset = ((long)page - 1) * pageSize;
        if (offset >= total)
            return new PagedResponse<T>([], page, pageSize, total);

        var items = await query.Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return new PagedResponse<T>(items, page, pageSize, total);
    }
}
