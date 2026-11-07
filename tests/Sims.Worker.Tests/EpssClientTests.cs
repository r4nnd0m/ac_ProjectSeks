using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Worker.Enrichment;
using static Sims.Worker.Tests.EnrichmentHttpStub;

namespace Sims.Worker.Tests;

public sealed class EpssClientTests
{
    [Fact]
    public async Task DeduplicatesAndBatches51CvesAndPreservesMissingScores()
    {
        var cves = Enumerable.Range(1000, 51).Select(i => $"CVE-2026-{i}").ToArray();
        var batches = new List<string[]>();
        using var http = Client((request, _) =>
        {
            Assert.Equal("/data/v1/epss", request.RequestUri!.AbsolutePath);
            Assert.Contains("&limit=50&offset=0", request.RequestUri.Query);
            var requested = ReadCves(request);
            batches.Add(requested);
            return Task.FromResult(Epss((requested[0].ToLowerInvariant(), "0.25")));
        });
        var inputs = cves.Concat([" cve-2026-1000 "]).ToArray();
        var scores = await Create(http).GetScoresAsync(inputs, CancellationToken.None);
        Assert.Equal(new[] { 50, 1 }, batches.Select(b => b.Length));
        Assert.Equal(cves, batches.SelectMany(b => b));
        Assert.Equal(2, scores.Count);
        Assert.Equal(0.25, scores["cve-2026-1000"]);
        Assert.Equal(0.25, scores["CVE-2026-1050"]);
        Assert.False(scores.ContainsKey("CVE-2026-1001"));
    }

    [Fact]
    public async Task AlsoRespectsThe2000CharacterParameterLimit()
    {
        var cves = Enumerable.Range(1, 51).Select(i => "CVE-2026-" + i.ToString(CultureInfo.InvariantCulture).PadLeft(41, '0')).ToArray();
        var sizes = new List<int>();
        using var http = Client((request, _) =>
        {
            var batch = ReadCves(request);
            Assert.InRange(string.Join(',', batch).Length, 1, 2000);
            sizes.Add(batch.Length);
            return Task.FromResult(Epss());
        });
        Assert.Empty(await Create(http).GetScoresAsync(cves, CancellationToken.None));
        Assert.Equal(new[] { 39, 12 }, sizes);
    }

    [Theory]
    [InlineData("0", 0d)]
    [InlineData("0.125", 0.125)]
    [InlineData("1", 1d)]
    public async Task ReadsDecimalPointsEvenWithAustrianCulture(string text, double expected)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-AT");
        try
        {
            using var http = Client((_, _) => Task.FromResult(Epss(("CVE-2026-1000", text))));
            var scores = await Create(http).GetScoresAsync(["CVE-2026-1000"], CancellationToken.None);
            Assert.Equal(expected, scores["CVE-2026-1000"]);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1.1")]
    [InlineData("-0.1")]
    [InlineData("0,5")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("unknown")]
    public async Task RejectsInvalidScores(string? value)
    {
        using var http = Client((_, _) => Task.FromResult(Epss(("CVE-2026-1000", value))));
        await Assert.ThrowsAsync<EpssUnavailableException>(() => Create(http).GetScoresAsync(["CVE-2026-1000"], CancellationToken.None));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"status\":\"ERROR\",\"status-code\":200,\"total\":0,\"offset\":0,\"data\":[]}")]
    [InlineData("{\"status\":\"OK\",\"status-code\":500,\"total\":0,\"offset\":0,\"data\":[]}")]
    [InlineData("{\"status\":\"OK\",\"status-code\":200,\"total\":1,\"offset\":0,\"data\":[]}")]
    [InlineData("{\"status\":\"OK\",\"status-code\":200,\"total\":0,\"offset\":1,\"data\":[]}")]
    [InlineData("{\"status\":\"OK\",\"status-code\":200,\"total\":0,\"offset\":0,\"data\":null}")]
    [InlineData("{\"status\":\"OK\",\"status-code\":200,\"total\":1,\"offset\":0,\"data\":[null]}")]
    [InlineData("{\"status\":\"OK\",\"status-code\":200,\"total\":1,\"offset\":0,\"data\":[{}]}")]
    public async Task RejectsIncompleteOrErrorResponses(string json)
    {
        using var http = Client((_, _) => Task.FromResult(Json(json)));
        await Assert.ThrowsAsync<EpssUnavailableException>(() => Create(http).GetScoresAsync(["CVE-2026-1000"], CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsForeignAndDuplicateCves(bool duplicate)
    {
        using var http = Client((_, _) => Task.FromResult(duplicate
            ? Epss(("CVE-2026-1000", "0.1"), ("cve-2026-1000", "0.2"))
            : Epss(("CVE-2026-9999", "0.1"))));
        await Assert.ThrowsAsync<EpssUnavailableException>(() => Create(http).GetScoresAsync(["CVE-2026-1000"], CancellationToken.None));
    }

    [Fact]
    public async Task FailedLaterBatchDoesNotReturnTheEarlierScores()
    {
        var calls = 0;
        using var http = Client((_, _) => Task.FromResult(++calls == 1
            ? Epss(("CVE-2026-1000", "0.1")) : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var cves = Enumerable.Range(1000, 51).Select(i => $"CVE-2026-{i}").ToArray();
        await Assert.ThrowsAsync<EpssUnavailableException>(() => Create(http).GetScoresAsync(cves, CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("policy-timeout")]
    [InlineData("circuit")]
    public async Task ConvertsFailuresInsteadOfPretendingScoresAreMissing(string kind)
    {
        Exception failure = kind switch
        {
            "network" => new HttpRequestException(), "timeout" => new TaskCanceledException(),
            "policy-timeout" => new TimeoutRejectedException(), _ => new BrokenCircuitException()
        };
        using var http = Client((_, _) => Task.FromException<HttpResponseMessage>(failure));
        await Assert.ThrowsAsync<EpssUnavailableException>(() => Create(http).GetScoresAsync(["CVE-2026-1000"], CancellationToken.None));
    }

    [Fact]
    public async Task EmptyInputNeedsNoHttpAndInvalidIdentifiersNeverBecomeQueries()
    {
        using var http = Client((_, _) => throw new InvalidOperationException("No request expected."));
        var client = Create(http);
        Assert.Empty(await client.GetScoresAsync([], CancellationToken.None));
        foreach (var invalid in new[] { "", "CVE-1", "CVE-2026-1000&other=1", "CVE-2026-1000\nX", new string('a', 51) })
            await Assert.ThrowsAsync<ArgumentException>(() => client.GetScoresAsync([invalid], CancellationToken.None));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetScoresAsync([], new CancellationToken(true)));
    }

    [Fact]
    public async Task CallerCancellationRemainsCancellation()
    {
        using var stop = new CancellationTokenSource();
        using var http = Client((_, ct) =>
        {
            stop.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Epss());
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(http).GetScoresAsync(["CVE-2026-1000"], stop.Token));
    }

    [Fact]
    public async Task RegisteredClientRetriesAndUsesFirstEndpoint()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var calls = 0;
        services.AddEpssClient().ConfigurePrimaryHttpMessageHandler(() => new EnrichmentHttpStub((request, _) =>
        {
            Assert.Equal("https://api.first.org", request.RequestUri!.GetLeftPart(UriPartial.Authority));
            return Task.FromResult(++calls == 1 ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : Epss());
        }));
        using var provider = services.BuildServiceProvider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Assert.Empty(await provider.GetRequiredService<IEpssClient>().GetScoresAsync(["CVE-2026-1000"], timeout.Token));
        Assert.Equal(2, calls);
    }

    private static string[] ReadCves(HttpRequestMessage request) =>
        Uri.UnescapeDataString(request.RequestUri!.Query.Split('&')[0]["?cve=".Length..]).Split(',');
    private static EpssClient Create(HttpClient http) => new(http, NullLogger<EpssClient>.Instance);
}
