using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Polly;

namespace Worker.Enrichment;

public sealed class EpssClient(HttpClient http, ILogger<EpssClient> logger) : IEpssClient
{
    public async Task<Dictionary<string, double>> GetScoresAsync(IReadOnlyList<string> cves, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cves);
        ct.ThrowIfCancellationRequested();
        if (cves.Any(cve => !CveIdentifier.IsValid(cve?.Trim())))
            throw new ArgumentException("EPSS benötigt gültige CVE-Kennungen.", nameof(cves));
        var requested = cves.Select(cve => cve.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var chunk in Batches(requested))
            {
                var query = Uri.EscapeDataString(string.Join(',', chunk));
                using var response = await http.GetAsync($"data/v1/epss?cve={query}&limit=50&offset=0", ct);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<EpssResponse>(ct);
                if (result?.Data is null || result.Status != "OK" || result.StatusCode != 200
                    || result.Offset != 0 || result.Total != result.Data.Count)
                    throw new EpssUnavailableException("Die EPSS-Antwort ist fehlerhaft oder unvollständig.");

                var chunkIds = new HashSet<string>(chunk, StringComparer.OrdinalIgnoreCase);
                foreach (var entry in result.Data)
                {
                    if (entry?.Cve is null || !chunkIds.Contains(entry.Cve)
                        || !double.TryParse(entry.Epss, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                        || !double.IsFinite(value) || value is < 0 or > 1
                        || !scores.TryAdd(entry.Cve.ToUpperInvariant(), value))
                        throw new EpssUnavailableException("EPSS liefert einen ungültigen oder nicht zuordenbaren Wert.");
                }
            }
            ct.ThrowIfCancellationRequested();
            logger.LogInformation("EPSS-Abfrage abgeschlossen: {Requested} CVEs, {Available} Werte verfügbar.",
                requested.Length, scores.Count);
            return scores;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException
                                   or OperationCanceledException or ExecutionRejectedException)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogWarning("EPSS-Abfrage fehlgeschlagen ({ErrorType}). Kein vollständiges Ergebnis verfügbar.", ex.GetType().Name);
            throw new EpssUnavailableException("EPSS konnte nicht vollständig abgefragt werden.");
        }
    }

    private static IEnumerable<string[]> Batches(IEnumerable<string> cves)
    {
        var batch = new List<string>();
        var length = 0;
        foreach (var cve in cves)
        {
            // Die FIRST-API erlaubt höchstens 2000 Zeichen im cve-Parameter.
            if (batch.Count == 50 || length + 1 + cve.Length > 2000)
            {
                yield return batch.ToArray();
                batch.Clear();
                length = 0;
            }
            length += cve.Length + (batch.Count == 0 ? 0 : 1);
            batch.Add(cve);
        }
        if (batch.Count > 0) yield return batch.ToArray();
    }
}
