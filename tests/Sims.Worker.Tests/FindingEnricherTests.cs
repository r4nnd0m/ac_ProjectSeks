using Worker.Enrichment;
using Worker.Osv;

namespace Sims.Worker.Tests;

public sealed class FindingEnricherTests
{
    [Fact]
    public async Task PreservesAllFindingsAndKeepsAbsentScoresDifferentFromZero()
    {
        var findings = new[] { Finding("CVE-2026-1000"), Finding("cve-2026-1000"), Finding("CVE-2026-2000"), Finding(null) };
        var kev = new KevStub(_ => Task.FromResult(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CVE-2026-1000" }));
        var epss = new EpssStub((cves, _) =>
        {
            Assert.Equal(new[] { "CVE-2026-1000", "CVE-2026-2000" }, cves);
            return Task.FromResult(new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["CVE-2026-1000"] = 0 });
        });
        var result = await new FindingEnricher(kev, epss).EnrichAsync(findings, CancellationToken.None);
        Assert.Equal(findings, result.Select(f => f.Finding));
        Assert.All(result.Take(2), f => { Assert.True(f.IsKev); Assert.Equal(0d, f.Epss); });
        Assert.All(result.Skip(2), f => { Assert.False(f.IsKev); Assert.Null(f.Epss); });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnySourceFailurePreventsAResult(bool failKev)
    {
        var kev = new KevStub(_ => failKev ? throw new KevUnavailableException("Test") : Task.FromResult(new HashSet<string>()));
        var epss = new EpssStub((_, _) => throw new EpssUnavailableException("Test"));
        var enricher = new FindingEnricher(kev, epss);
        if (failKev)
            await Assert.ThrowsAsync<KevUnavailableException>(() => enricher.EnrichAsync([Finding("CVE-2026-1000")], CancellationToken.None));
        else
            await Assert.ThrowsAsync<EpssUnavailableException>(() => enricher.EnrichAsync([Finding("CVE-2026-1000")], CancellationToken.None));
    }

    [Fact]
    public async Task StopSignalIsForwardedAndCheckedAfterTheLastSource()
    {
        using var stop = new CancellationTokenSource();
        var kev = new KevStub(ct => { Assert.Equal(stop.Token, ct); return Task.FromResult(new HashSet<string>()); });
        var epss = new EpssStub((_, ct) =>
        {
            Assert.Equal(stop.Token, ct);
            stop.Cancel();
            return Task.FromResult(new Dictionary<string, double>());
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FindingEnricher(kev, epss)
            .EnrichAsync([Finding(null)], stop.Token));
    }

    private static OsvFinding Finding(string? cve) => new(Guid.NewGuid(), "GHSA-example", cve, OsvSeverity.High, "Example");
    private sealed class KevStub(Func<CancellationToken, Task<HashSet<string>>> get) : IKevClient
    {
        public Task<HashSet<string>> GetKnownExploitedAsync(CancellationToken ct) => get(ct);
    }
    private sealed class EpssStub(Func<IReadOnlyList<string>, CancellationToken, Task<Dictionary<string, double>>> get) : IEpssClient
    {
        public Task<Dictionary<string, double>> GetScoresAsync(IReadOnlyList<string> cves, CancellationToken ct) => get(cves, ct);
    }
}
