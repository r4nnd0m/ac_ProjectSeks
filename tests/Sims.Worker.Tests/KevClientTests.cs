using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Worker.Enrichment;
using static Sims.Worker.Tests.EnrichmentHttpStub;

namespace Sims.Worker.Tests;

public sealed class KevClientTests
{
    [Fact]
    public async Task CachesForSixHoursWithoutSlidingAndProtectsTheSharedSet()
    {
        var clock = new FakeTimeProvider();
        using var memory = new MemoryCache(new MemoryCacheOptions { Clock = new CacheClock(clock) });
        using var cache = new KevCatalogCache(memory);
        var calls = 0;
        using var http = Client((_, _) => { calls++; return Task.FromResult(Kev("CVE-2026-1000", "cve-2026-1000")); });
        var client = Create(http, cache);

        var first = await client.GetKnownExploitedAsync(CancellationToken.None);
        Assert.Single(first);
        Assert.Contains("cve-2026-1000", first);
        first.Clear();
        clock.Advance(TimeSpan.FromHours(6) - TimeSpan.FromSeconds(1));
        Assert.Single(await client.GetKnownExploitedAsync(CancellationToken.None));
        Assert.Equal(1, calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetKnownExploitedAsync(new CancellationToken(true)));

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Single(await client.GetKnownExploitedAsync(CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task FailedRefreshDoesNotReturnStaleDataOrCacheTheFailure()
    {
        var clock = new FakeTimeProvider();
        using var memory = new MemoryCache(new MemoryCacheOptions { Clock = new CacheClock(clock) });
        using var cache = new KevCatalogCache(memory);
        var calls = 0;
        using var http = Client((_, _) => Task.FromResult(++calls == 2
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Kev(calls == 1 ? "CVE-2026-1000" : "CVE-2026-2000")));
        var client = Create(http, cache);
        await client.GetKnownExploitedAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromHours(6));
        await Assert.ThrowsAsync<KevUnavailableException>(() => client.GetKnownExploitedAsync(CancellationToken.None));
        var fresh = await client.GetKnownExploitedAsync(CancellationToken.None);
        Assert.Equal("CVE-2026-2000", Assert.Single(fresh));
        await client.GetKnownExploitedAsync(CancellationToken.None);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ConcurrentClientsShareOneFetchAndWaitingCallerCanCancel()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new KevCatalogCache(memory);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var http = Client(async (_, ct) =>
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await release.Task.WaitAsync(ct);
            return Kev("CVE-2026-1000");
        });
        var first = Create(http, cache).GetKnownExploitedAsync(CancellationToken.None);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            using var stop = new CancellationTokenSource();
            var cancelled = Create(http, cache).GetKnownExploitedAsync(stop.Token);
            stop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            var second = Create(http, cache).GetKnownExploitedAsync(CancellationToken.None);
            Assert.Equal(1, calls);
            release.TrySetResult();
            Assert.Single(await second.WaitAsync(TimeSpan.FromSeconds(3)));
        }
        finally { release.TrySetResult(); }
        Assert.Single(await first.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"vulnerabilities\":[]}")]
    [InlineData("{\"count\":1}")]
    [InlineData("{\"count\":0,\"vulnerabilities\":[]}")]
    [InlineData("{\"count\":1,\"vulnerabilities\":null}")]
    [InlineData("{\"count\":2,\"vulnerabilities\":[{\"cveID\":\"CVE-2026-1000\"}]}")]
    [InlineData("{\"count\":1,\"vulnerabilities\":[null]}")]
    [InlineData("{\"count\":1,\"vulnerabilities\":[{}]}")]
    [InlineData("{\"count\":1,\"vulnerabilities\":[{\"cveID\":\"bad\"}]}")]
    public async Task RejectsInvalidCatalogsAndRetriesOnTheNextCall(string json)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new KevCatalogCache(memory);
        var calls = 0;
        using var http = Client((_, _) => Task.FromResult(++calls == 1 ? Json(json) : Kev("CVE-2026-1000")));
        var client = Create(http, cache);
        await Assert.ThrowsAsync<KevUnavailableException>(() => client.GetKnownExploitedAsync(CancellationToken.None));
        Assert.Single(await client.GetKnownExploitedAsync(CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("policy-timeout")]
    [InlineData("circuit")]
    public async Task SourceFailuresAreNotAnEmptyCatalog(string kind)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new KevCatalogCache(memory);
        Exception failure = kind switch
        {
            "network" => new HttpRequestException(), "timeout" => new TaskCanceledException(),
            "policy-timeout" => new TimeoutRejectedException(), _ => new BrokenCircuitException()
        };
        using var http = Client((_, _) => Task.FromException<HttpResponseMessage>(failure));
        await Assert.ThrowsAsync<KevUnavailableException>(() => Create(http, cache).GetKnownExploitedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CancellationDuringFetchReleasesTheGateAndDoesNotPopulateTheCache()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new KevCatalogCache(memory);
        using var stop = new CancellationTokenSource();
        var calls = 0;
        using var http = Client((_, ct) =>
        {
            if (++calls == 1) { stop.Cancel(); ct.ThrowIfCancellationRequested(); }
            return Task.FromResult(Kev("CVE-2026-1000"));
        });
        var client = Create(http, cache);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetKnownExploitedAsync(stop.Token));
        Assert.Single(await client.GetKnownExploitedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RegistrationRetriesAndSharesCacheAcrossResolvedClients()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var calls = 0;
        services.AddKevClient().ConfigurePrimaryHttpMessageHandler(() => new EnrichmentHttpStub((request, _) =>
        {
            Assert.Equal("https://www.cisa.gov/sites/default/files/feeds/known_exploited_vulnerabilities.json", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(++calls == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Kev("CVE-2026-1000"));
        }));
        using var provider = services.BuildServiceProvider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Assert.Single(await provider.GetRequiredService<IKevClient>().GetKnownExploitedAsync(timeout.Token));
        Assert.Single(await provider.GetRequiredService<IKevClient>().GetKnownExploitedAsync(timeout.Token));
        Assert.Equal(2, calls);
    }

    private static KevClient Create(HttpClient http, KevCatalogCache cache) => new(http, cache, NullLogger<KevClient>.Instance);
    private sealed class CacheClock(FakeTimeProvider clock) : ISystemClock
    {
        public DateTimeOffset UtcNow => clock.GetUtcNow();
    }
}
