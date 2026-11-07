using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;
using Worker.Osv;

namespace Sims.Worker.Tests;

public sealed class OsvClientTests
{
    [Fact]
    public async Task KeepsComponentOrderAndFetchesSharedDetailsOnlyOnce()
    {
        var components = new[] { Component("pkg:nuget/First@1"), Component("pkg:nuget/Second@2") };
        var details = new ConcurrentBag<string>();
        using var http = Http(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal("/v1/querybatch", request.RequestUri!.AbsolutePath);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                var queries = body.RootElement.GetProperty("queries");
                Assert.Equal(components.Select(c => c.Purl), queries.EnumerateArray()
                    .Select(q => q.GetProperty("package").GetProperty("purl").GetString()));
                Assert.All(queries.EnumerateArray(), q => Assert.False(q.TryGetProperty("version", out _)));
                return Json("""{"results":[{"vulns":[{"id":"GHSA-A"},{"id":"GHSA-A"}]},{"vulns":[{"id":"GHSA-B"},{"id":"GHSA-A"}]}]}""");
            }
            var id = request.RequestUri!.Segments.Last();
            details.Add(id);
            return Json(JsonSerializer.Serialize(new
            {
                id, aliases = new[] { "OTHER", "CVE-2026-1234" }, summary = "Example",
                database_specific = new { severity = "HIGH" }
            }));
        });

        var result = await Client(http).FindVulnerabilitiesAsync(components, CancellationToken.None);

        Assert.Equal(new[] { components[0].Id, components[1].Id, components[1].Id }, result.Select(f => f.ComponentId));
        Assert.Equal(new[] { "GHSA-A", "GHSA-B", "GHSA-A" }, result.Select(f => f.VulnId));
        Assert.Equal(2, details.Count);
        Assert.All(result, f => { Assert.Equal("CVE-2026-1234", f.Cve); Assert.Equal(OsvSeverity.High, f.Severity); });
    }

    [Fact]
    public async Task Splits501ComponentsAndKeepsTheBoundaryMapping()
    {
        var components = Enumerable.Range(0, 501).Select(i => Component($"pkg:nuget/Example{i}@1")).ToArray();
        var sizes = new List<int>();
        using var http = Http(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
                return Json("""{"id":"GHSA-BOUNDARY"}""");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var queries = body.RootElement.GetProperty("queries");
            sizes.Add(queries.GetArrayLength());
            return Json(JsonSerializer.Serialize(new { results = queries.EnumerateArray().Select(q =>
                new { vulns = q.GetProperty("package").GetProperty("purl").GetString() == components[500].Purl
                    ? new[] { new { id = "GHSA-BOUNDARY" } } : [] }) }));
        });

        var result = await Client(http).FindVulnerabilitiesAsync(components, CancellationToken.None);
        Assert.Equal(new[] { 500, 1 }, sizes);
        Assert.Equal(components[500].Id, Assert.Single(result).ComponentId);
    }

    [Theory]
    [InlineData("CRITICAL", OsvSeverity.Critical)]
    [InlineData("high", OsvSeverity.High)]
    [InlineData(" LOW ", OsvSeverity.Low)]
    [InlineData("MODERATE", OsvSeverity.Medium)]
    [InlineData("MEDIUM", OsvSeverity.Medium)]
    [InlineData("unknown", OsvSeverity.Medium)]
    [InlineData(null, OsvSeverity.Medium)]
    public async Task MapsSeverityAndPrefersTheCveId(string? severity, OsvSeverity expected)
    {
        using var http = Http((request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? Json("""{"results":[{"vulns":[{"id":"CVE-2026-1234"}]}]}""")
            : Json(JsonSerializer.Serialize(new
            {
                id = "CVE-2026-1234", aliases = new[] { "CVE-2026-9999" },
                summary = new string('x', 501), database_specific = new { severity }
            }))));

        var finding = Assert.Single(await Client(http).FindVulnerabilitiesAsync([Component()], CancellationToken.None));
        Assert.Equal("CVE-2026-1234", finding.Cve);
        Assert.Equal(expected, finding.Severity);
        Assert.Equal(500, finding.Summary!.Length);
    }

    [Fact]
    public async Task AllowsMissingOptionalDetailsAndDoesNotSplitUnicode()
    {
        using var http = Http((request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? Json("""{"results":[{"vulns":[{"id":"GHSA-A"},{"id":"GHSA-B"}]}]}""")
            : request.RequestUri!.AbsolutePath.EndsWith("GHSA-A", StringComparison.Ordinal)
                ? Json("""{"id":"GHSA-A"}""")
                : Json(JsonSerializer.Serialize(new { id = "GHSA-B", aliases = new string?[] { null, "OTHER" }, summary = new string('x', 499) + "😀end" }))));
        var result = await Client(http).FindVulnerabilitiesAsync([Component()], CancellationToken.None);
        Assert.All(result, f => { Assert.Null(f.Cve); Assert.Equal(OsvSeverity.Medium, f.Severity); });
        Assert.Null(result[0].Summary);
        Assert.Equal(new string('x', 499), result[1].Summary);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"results\":null}")]
    [InlineData("{\"results\":[]}")]
    [InlineData("{\"results\":[null]}")]
    [InlineData("{\"results\":[{\"error\":\"unavailable\"}]}")]
    [InlineData("{\"results\":[{},{}]}")]
    [InlineData("{\"results\":[{\"vulns\":[{}]}]}")]
    [InlineData("{\"results\":[{\"vulns\":[null]}]}")]
    [InlineData("{\"results\":[{\"vulns\":[{\"id\":\"\"}]}]}")]
    [InlineData("{\"results\":[{\"next_page_token\":\"more\"}]}")]
    [InlineData("{\"results\":[{\"vulns\":[{\"id\":\"GHSA-A\"}],\"next_page_token\":\"more\"}]}")]
    public async Task RejectsIncompleteOrInvalidBatches(string response)
    {
        using var http = Http((_, _) => Task.FromResult(Json(response)));
        await Assert.ThrowsAsync<OsvUnavailableException>(() => Client(http)
            .FindVulnerabilitiesAsync([Component()], CancellationToken.None));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"id\":\"WRONG\"}")]
    [InlineData("not-json")]
    public async Task RejectsInvalidDetailsInsteadOfReturningPartialResults(string invalid)
    {
        using var http = Http((request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? Json("""{"results":[{"vulns":[{"id":"GHSA-A"},{"id":"GHSA-B"}]}]}""")
            : request.RequestUri!.AbsolutePath.EndsWith("GHSA-A", StringComparison.Ordinal)
                ? Json("""{"id":"GHSA-A"}""") : Json(invalid)));
        await Assert.ThrowsAsync<OsvUnavailableException>(() => Client(http)
            .FindVulnerabilitiesAsync([Component()], CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpErrorsInBatchOrDetailsAreNotCleanResults(bool failDetails)
    {
        using var http = Http((request, _) => Task.FromResult(failDetails && request.Method == HttpMethod.Post
            ? Json("""{"results":[{"vulns":[{"id":"GHSA-A"}]}]}""")
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await Assert.ThrowsAsync<OsvUnavailableException>(() => Client(http)
            .FindVulnerabilitiesAsync([Component()], CancellationToken.None));
    }

    [Theory]
    [InlineData("network")]
    [InlineData("stream")]
    [InlineData("http-timeout")]
    [InlineData("policy-timeout")]
    [InlineData("circuit")]
    [InlineData("rate-limit")]
    public async Task ConvertsTransportAndResilienceFailures(string kind)
    {
        Exception failure = kind switch
        {
            "network" => new HttpRequestException(),
            "stream" => new IOException(),
            "http-timeout" => new TaskCanceledException(),
            "policy-timeout" => new TimeoutRejectedException(),
            "circuit" => new BrokenCircuitException(),
            _ => new RateLimiterRejectedException()
        };
        using var http = Http((_, _) => Task.FromException<HttpResponseMessage>(failure));
        await Assert.ThrowsAsync<OsvUnavailableException>(() => Client(http)
            .FindVulnerabilitiesAsync([Component()], CancellationToken.None));
    }

    [Fact]
    public async Task CancellationRemainsCancellation()
    {
        using var stop = new CancellationTokenSource();
        using var http = Http((_, ct) =>
        {
            stop.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Json("{}"));
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http)
            .FindVulnerabilitiesAsync([Component()], stop.Token));
    }

    [Fact]
    public async Task NoFindingsIsAllowedOnlyForAValidEmptyResult()
    {
        using var http = Http((_, _) => Task.FromResult(Json("""{"results":[{}]}""")));
        Assert.Empty(await Client(http).FindVulnerabilitiesAsync([Component()], CancellationToken.None));
    }

    [Fact]
    public async Task EmptyInputNeedsNoNetworkAndInvalidInputIsRejected()
    {
        using var http = Http((_, _) => throw new InvalidOperationException("No request expected."));
        var client = Client(http);
        Assert.Empty(await client.FindVulnerabilitiesAsync([], CancellationToken.None));
        var component = Component();
        await Assert.ThrowsAsync<ArgumentException>(() => client.FindVulnerabilitiesAsync([component, component], CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => client.FindVulnerabilitiesAsync([new(Guid.Empty, "pkg:nuget/A@1")], CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => client.FindVulnerabilitiesAsync([new(Guid.NewGuid(), "")], CancellationToken.None));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.FindVulnerabilitiesAsync([], new CancellationToken(true)));
    }

    [Fact]
    public async Task LimitsConcurrentDetailRequestsToFive()
    {
        var fiveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var started = 0;
        var maximum = 0;
        var gate = new object();
        using var http = Http(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Post)
                return Json(JsonSerializer.Serialize(new { results = new[] { new { vulns = Enumerable.Range(0, 12).Select(i => new { id = $"GHSA-{i}" }) } } }));
            lock (gate)
            {
                maximum = Math.Max(maximum, ++active);
                if (++started == 5) fiveStarted.TrySetResult();
            }
            try
            {
                await release.Task.WaitAsync(ct);
                return Json(JsonSerializer.Serialize(new { id = request.RequestUri!.Segments.Last() }));
            }
            finally { lock (gate) active--; }
        });
        var task = Client(http).FindVulnerabilitiesAsync([Component()], CancellationToken.None);
        try
        {
            await fiveStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(5, Volatile.Read(ref started));
        }
        finally { release.TrySetResult(); }
        Assert.Equal(12, (await task.WaitAsync(TimeSpan.FromSeconds(3))).Count);
        Assert.Equal(5, maximum);
    }

    [Fact]
    public async Task RegisteredClientRetriesTransientBatchAndDetailFailures()
    {
        var batchCalls = 0;
        var detailCalls = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOsvClient().ConfigurePrimaryHttpMessageHandler(() => new Handler((request, _) =>
        {
            Assert.Equal("https://api.osv.dev", request.RequestUri!.GetLeftPart(UriPartial.Authority));
            if (request.Method == HttpMethod.Post)
                return Task.FromResult(++batchCalls == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : Json("""{"results":[{"vulns":[{"id":"GHSA-A"}]}]}"""));
            return Task.FromResult(++detailCalls == 1 ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : Json("""{"id":"GHSA-A"}"""));
        }));
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IOsvClient>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Assert.Single(await client.FindVulnerabilitiesAsync([Component()], timeout.Token));
        Assert.Equal(2, batchCalls);
        Assert.Equal(2, detailCalls);
    }

    private static OsvComponent Component(string purl = "pkg:nuget/Example@1") => new(Guid.NewGuid(), purl);
    private static OsvClient Client(HttpClient http) => new(http, NullLogger<OsvClient>.Instance);
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
    private static HttpClient Http(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new Handler(send)) { BaseAddress = new Uri("https://api.osv.dev/") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
}
