using System.Text.Json.Serialization;

namespace Worker.Enrichment;

internal sealed record KevCatalog(
    [property: JsonRequired] int Count,
    [property: JsonRequired] List<KevEntry?>? Vulnerabilities);
internal sealed record KevEntry([property: JsonRequired] string? CveID);

internal sealed record EpssResponse(
    [property: JsonRequired] string? Status,
    [property: JsonRequired, JsonPropertyName("status-code")] int StatusCode,
    [property: JsonRequired] int Total,
    [property: JsonRequired] int Offset,
    [property: JsonRequired] List<EpssEntry?>? Data);
internal sealed record EpssEntry(
    [property: JsonRequired] string? Cve,
    [property: JsonRequired] string? Epss);
