using System.Text.Json.Serialization;

namespace Worker.Osv;

internal sealed record OsvBatchRequest(List<OsvQuery> Queries);
internal sealed record OsvQuery(OsvPackage Package);
internal sealed record OsvPackage(string Purl);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record OsvBatchResponse(
    [property: JsonRequired] List<OsvBatchResult?>? Results);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record OsvBatchResult(
    List<OsvVulnRef?>? Vulns,
    [property: JsonPropertyName("next_page_token")] string? NextPageToken);
internal sealed record OsvVulnRef([property: JsonRequired] string? Id);
internal sealed record OsvVuln(
    [property: JsonRequired] string? Id,
    List<string?>? Aliases,
    string? Summary,
    [property: JsonPropertyName("database_specific")] OsvDatabaseSpecific? DatabaseSpecific);
internal sealed record OsvDatabaseSpecific(string? Severity);
