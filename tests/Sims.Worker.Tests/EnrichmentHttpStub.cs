using System.Net;
using System.Text;
using System.Text.Json;

namespace Sims.Worker.Tests;

internal sealed class EnrichmentHttpStub(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);

    internal static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new EnrichmentHttpStub(send)) { BaseAddress = new Uri("https://example.test/") };

    internal static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    internal static HttpResponseMessage Kev(params string[] cves) => Json(JsonSerializer.Serialize(new
    {
        count = cves.Length, vulnerabilities = cves.Select(cve => new { cveID = cve })
    }));

    internal static HttpResponseMessage Epss(params (string Cve, string? Score)[] entries) =>
        Json(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["status"] = "OK", ["status-code"] = 200, ["total"] = entries.Length, ["offset"] = 0,
            ["data"] = entries.Select(e => new { cve = e.Cve, epss = e.Score }).ToArray()
        }));
}
