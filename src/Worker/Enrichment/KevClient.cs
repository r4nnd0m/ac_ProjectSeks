using System.Net.Http.Json;
using System.Text.Json;
using Polly;

namespace Worker.Enrichment;

public sealed class KevClient(HttpClient http, KevCatalogCache cache, ILogger<KevClient> logger) : IKevClient
{
    public Task<HashSet<string>> GetKnownExploitedAsync(CancellationToken ct) => cache.GetOrLoadAsync(LoadAsync, ct);

    private async Task<HashSet<string>> LoadAsync(CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync("sites/default/files/feeds/known_exploited_vulnerabilities.json", ct);
            response.EnsureSuccessStatusCode();
            var catalog = await response.Content.ReadFromJsonAsync<KevCatalog>(ct);
            if (catalog?.Vulnerabilities is null || catalog.Count <= 0 || catalog.Count != catalog.Vulnerabilities.Count)
                throw new KevUnavailableException("Der KEV-Katalog ist leer oder unvollständig.");

            var cves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in catalog.Vulnerabilities)
            {
                if (!CveIdentifier.IsValid(entry?.CveID))
                    throw new KevUnavailableException("Der KEV-Katalog enthält eine ungültige CVE-Kennung.");
                cves.Add(entry!.CveID!.ToUpperInvariant());
            }
            logger.LogInformation("KEV-Katalog geladen: {Count} CVEs. Cache für 6 Stunden gültig.", cves.Count);
            return cves;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException
                                   or OperationCanceledException or ExecutionRejectedException)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogWarning("KEV-Abruf fehlgeschlagen ({ErrorType}). Kein vollständiger Katalog verfügbar.", ex.GetType().Name);
            throw new KevUnavailableException("Der KEV-Katalog konnte nicht vollständig geladen werden.");
        }
    }
}
