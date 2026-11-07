using Microsoft.Extensions.Caching.Memory;

namespace Worker.Enrichment;

/// <summary>Ein gemeinsamer Cache für alle kurzlebigen KevClient-Instanzen.</summary>
public sealed class KevCatalogCache(IMemoryCache cache) : IDisposable
{
    private readonly object cacheKey = new();
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    internal async Task<HashSet<string>> GetOrLoadAsync(
        Func<CancellationToken, Task<HashSet<string>>> load, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (cache.TryGetValue<HashSet<string>>(cacheKey, out var existing))
            return new HashSet<string>(existing!, StringComparer.OrdinalIgnoreCase);

        // Mehrere gleichzeitige Aufrufer sollen den Katalog nicht mehrfach laden.
        await refreshGate.WaitAsync(ct);
        try
        {
            if (!cache.TryGetValue<HashSet<string>>(cacheKey, out existing))
            {
                existing = await load(ct);
                ct.ThrowIfCancellationRequested();
                cache.Set(cacheKey, existing, TimeSpan.FromHours(6));
            }
            // Der Aufrufer darf seine Menge ändern, ohne den Cache für andere zu verändern.
            return new HashSet<string>(existing!, StringComparer.OrdinalIgnoreCase);
        }
        finally { refreshGate.Release(); }
    }

    public void Dispose() => refreshGate.Dispose();
}
