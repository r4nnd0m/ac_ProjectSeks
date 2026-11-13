using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Polly;

namespace Worker.Osv;

public sealed class OsvClient(HttpClient http, ILogger<OsvClient> logger) : IOsvClient
{
    public async Task<IReadOnlyList<OsvFinding>> FindVulnerabilitiesAsync(
        IReadOnlyList<OsvComponent> components, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(components);
        ct.ThrowIfCancellationRequested();
        ValidateComponents(components);
        if (components.Count == 0)
            return [];

        try
        {
            var pairs = await QueryBatchesAsync(components, ct);
            var details = new ConcurrentDictionary<string, OsvVuln>(StringComparer.Ordinal);

            // Details pro Schwachstelle nur einmal laden, auch wenn mehrere Komponenten betroffen sind.
            await Parallel.ForEachAsync(pairs.Select(p => p.VulnId).Distinct(StringComparer.Ordinal),
                new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = ct },
                async (id, token) =>
                {
                    using var response = await http.GetAsync($"v1/vulns/{Uri.EscapeDataString(id)}", token);
                    response.EnsureSuccessStatusCode();
                    var detail = await response.Content.ReadFromJsonAsync<OsvVuln>(token);
                    if (detail is null || !string.Equals(detail.Id, id, StringComparison.Ordinal))
                        throw new OsvUnavailableException("Die OSV-Details passen nicht zur angefragten Schwachstelle.");
                    details[id] = detail;
                });

            ct.ThrowIfCancellationRequested();
            var findings = pairs.Select(pair => MapFinding(pair.ComponentId, details[pair.VulnId])).ToArray();
            logger.LogInformation("OSV-Abfrage abgeschlossen: {Components} Komponenten, {Findings} Funde.",
                components.Count, findings.Length);
            return findings;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException
                                   or OperationCanceledException or ExecutionRejectedException)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogWarning("OSV-Abfrage fehlgeschlagen ({ErrorType}). Kein vollständiges Ergebnis verfügbar.",
                ex.GetType().Name);
            throw new OsvUnavailableException("OSV ist nicht erreichbar oder hat keine auswertbare Antwort geliefert.");
        }
    }

    private async Task<List<(Guid ComponentId, string VulnId)>> QueryBatchesAsync(
        IReadOnlyList<OsvComponent> components, CancellationToken ct)
    {
        var pairs = new List<(Guid ComponentId, string VulnId)>();
        var seen = new HashSet<(Guid ComponentId, string VulnId)>();

        foreach (var chunk in components.Chunk(500))
        {
            var request = new OsvBatchRequest(chunk.Select(c => new OsvQuery(new OsvPackage(c.Purl))).ToList());
            using var response = await http.PostAsJsonAsync("v1/querybatch", request, ct);
            response.EnsureSuccessStatusCode();
            var batch = await response.Content.ReadFromJsonAsync<OsvBatchResponse>(ct);
            if (batch?.Results is null || batch.Results.Count != chunk.Length)
                throw new OsvUnavailableException("Die Anzahl der OSV-Ergebnisse passt nicht zur Anfrage.");

            for (var i = 0; i < chunk.Length; i++)
            {
                var result = batch.Results[i]
                    ?? throw new OsvUnavailableException("Ein OSV-Ergebnis fehlt.");
                // Paging wird nicht unterstützt, also lieber abbrechen als Funde verlieren.
                if (!string.IsNullOrEmpty(result.NextPageToken))
                    throw new OsvUnavailableException("OSV liefert weitere Ergebnisseiten. Die Abfrage ist unvollständig.");

                foreach (var vuln in result.Vulns ?? [])
                {
                    if (string.IsNullOrWhiteSpace(vuln?.Id))
                        throw new OsvUnavailableException("OSV liefert eine Schwachstelle ohne Kennung.");
                    var pair = (chunk[i].Id, vuln.Id);
                    if (seen.Add(pair))
                        pairs.Add(pair);
                }
            }
        }

        return pairs;
    }

    private static OsvFinding MapFinding(Guid componentId, OsvVuln vuln)
    {
        var id = vuln.Id!;
        var cve = id.StartsWith("CVE-", StringComparison.OrdinalIgnoreCase)
            ? id
            : vuln.Aliases?.FirstOrDefault(a => a?.StartsWith("CVE-", StringComparison.OrdinalIgnoreCase) == true);
        var severity = vuln.DatabaseSpecific?.Severity?.Trim().ToUpperInvariant() switch
        {
            "CRITICAL" => OsvSeverity.Critical,
            "HIGH" => OsvSeverity.High,
            "LOW" => OsvSeverity.Low,
            "MODERATE" or "MEDIUM" => OsvSeverity.Medium,
            _ => OsvSeverity.Medium
        };
        var summary = vuln.Summary;
        if (summary?.Length > 500)
        {
            // Surrogatpaar nicht in der Mitte abschneiden
            var length = char.IsHighSurrogate(summary[499]) ? 499 : 500;
            summary = summary[..length];
        }
        return new OsvFinding(componentId, id, cve, severity, summary);
    }

    private static void ValidateComponents(IReadOnlyList<OsvComponent> components)
    {
        var ids = new HashSet<Guid>();
        foreach (var component in components)
            if (component is null || component.Id == Guid.Empty || string.IsNullOrWhiteSpace(component.Purl)
                || !ids.Add(component.Id))
                throw new ArgumentException("Komponenten brauchen eindeutige IDs und eine nicht leere Purl.", nameof(components));
    }
}
