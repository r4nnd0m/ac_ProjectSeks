using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Worker;
using Worker.Enrichment;
using Worker.Osv;
using Worker.Scanning;

namespace Sims.Worker.Tests;

public sealed class ScanProcessorTests
{
    [Fact]
    public async Task SavesScanningBeforeQueriesAndCommitsAllResultsOnlyAfterPlanning()
    {
        var h = new Harness();
        using var stop = new CancellationTokenSource();
        var planAt = h.Clock.GetUtcNow();
        h.OnStage = stage => { if (stage == "tickets") h.Clock.Advance(TimeSpan.FromSeconds(2)); };
        await h.Processor.ProcessPendingAsync(stop.Token);
        Assert.Equal(new[] { "pending", "start", "components", "osv", "enrich", "verdict", "tickets", "complete" }, h.Events);
        var saved = Assert.Single(h.Completed).Value;
        Assert.Same(h.Enriched, saved.Findings);
        Assert.Same(h.TicketBatch, saved.Tickets);
        Assert.Equal(ScanVerdict.NoGo, saved.Verdict);
        Assert.Equal(planAt, h.PlannedAt);
        Assert.Equal(planAt.AddSeconds(2), saved.FinishedAt);
        Assert.All(h.Tokens, token => Assert.Equal(stop.Token, token));
        Assert.Empty(h.Failed);
    }

    [Fact]
    public async Task LoadsAtMostFiveOldestDistinctScansAndLeavesTheRestForNextCycle()
    {
        var h = new Harness(7);
        var expected = h.Scans.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToArray();
        h.Scans.Reverse();
        h.Scans.Add(h.Scans[0]); // Defensiv gegen doppelte Zeilen am Adapterrand.
        await h.Processor.ProcessPendingAsync(CancellationToken.None);
        Assert.Equal(5, h.RequestedLimit);
        Assert.Equal(expected.Take(5).Select(s => s.Id), h.Started);
        Assert.Equal(5, h.Completed.Count);
        await h.Processor.ProcessPendingAsync(CancellationToken.None);
        Assert.Equal(expected.Select(s => s.Id), h.Started);
        Assert.Equal(7, h.Completed.Count);
    }

    [Fact]
    public async Task SkipsScanThatIsNoLongerPendingAndDoesNotReprocessCompletedScans()
    {
        var h = new Harness(2);
        h.RejectStart = h.Scans[0].Id;
        await h.Processor.ProcessPendingAsync(CancellationToken.None);
        Assert.Single(h.Completed);
        Assert.DoesNotContain(h.RejectStart.Value, h.Completed.Keys);
        var calls = h.Events.Count(e => e == "osv");
        await h.Processor.ProcessPendingAsync(CancellationToken.None);
        Assert.Equal(calls, h.Events.Count(e => e == "osv"));
    }

    [Fact]
    public async Task EmptyQueueDoesNotCallAnySourceOrRule()
    {
        var h = new Harness(0);
        await h.Processor.ProcessPendingAsync(CancellationToken.None);
        Assert.Equal(new[] { "pending" }, h.Events);
    }

    [Fact]
    public async Task EmptyFindingsStillUseTheRulesAndCompleteNormally()
    {
        var h = new Harness { Findings = [], Enriched = [], Verdict = ScanVerdict.Go };
        await h.Processor.ProcessPendingAsync(CancellationToken.None);
        Assert.Empty(Assert.Single(h.Completed).Value.Findings);
        Assert.Contains("tickets", h.Events);
    }

    [Theory]
    [InlineData("components")]
    [InlineData("osv")]
    [InlineData("enrich")]
    [InlineData("verdict")]
    [InlineData("tickets")]
    public async Task PreparationFailureSavesOnlyFailedAndContinuesWithNextScan(string stage)
    {
        var h = new Harness(2);
        var failedId = h.Scans[0].Id;
        var injected = false;
        h.OnStage = current =>
        {
            if (current == stage && !injected) { injected = true; throw new InvalidOperationException("secret-detail"); }
        };
        await h.Processor.ProcessPendingAsync(CancellationToken.None);
        var failure = Assert.Single(h.Failed);
        Assert.Equal(failedId, failure.Key);
        Assert.Equal(ScanProcessor.FailureMessage, failure.Value.Message);
        Assert.Equal(h.Clock.GetUtcNow(), failure.Value.At);
        Assert.DoesNotContain(failedId, h.Completed.Keys);
        Assert.Single(h.Completed);
        Assert.DoesNotContain(h.Logs, message => message.Contains("secret-detail"));
    }

    [Theory]
    [InlineData("osv")]
    [InlineData("kev")]
    [InlineData("epss")]
    [InlineData("unrequested-cancellation")]
    public async Task SourceAndUnrequestedCancellationFailuresNeverProduceAnEmptyGo(string kind)
    {
        var h = new Harness();
        h.OnStage = stage =>
        {
            if (stage == "enrich") throw kind switch
            {
                "osv" => new OsvUnavailableException("secret-detail"),
                "kev" => new KevUnavailableException("secret-detail"),
                "epss" => new EpssUnavailableException("secret-detail"),
                _ => new OperationCanceledException("secret-detail")
            };
        };
        await h.Processor.ProcessPendingAsync(CancellationToken.None);
        Assert.Single(h.Failed);
        Assert.Empty(h.Completed);
        Assert.DoesNotContain("verdict", h.Events);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("start")]
    [InlineData("components")]
    [InlineData("osv")]
    [InlineData("enrich")]
    [InlineData("tickets")]
    [InlineData("complete")]
    public async Task RequestedCancellationDoesNotWriteFailedOrPartialResults(string stage)
    {
        var h = new Harness();
        using var stop = new CancellationTokenSource();
        h.OnStage = current => { if (current == stage) stop.Cancel(); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Processor.ProcessPendingAsync(stop.Token));
        Assert.Empty(h.Failed);
        Assert.Empty(h.Completed);
        Assert.DoesNotContain(h.Logs, message => message.Contains("fehlgeschlagen"));
    }

    [Fact]
    public async Task AlreadyCancelledTokenDoesNotTouchTheStore()
    {
        var h = new Harness();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Processor.ProcessPendingAsync(new CancellationToken(true)));
        Assert.Empty(h.Events);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("start")]
    [InlineData("complete")]
    public async Task PersistenceFailureEscapesWithoutBlindlyOverwritingTheScan(string stage)
    {
        var h = new Harness(2);
        h.OnStage = current => { if (current == stage) throw new InvalidOperationException("store-unavailable"); };
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Processor.ProcessPendingAsync(CancellationToken.None));
        Assert.Empty(h.Failed);
        Assert.Empty(h.Completed);
        Assert.True(h.Started.Count <= 1);
    }

    [Fact]
    public async Task UnknownCompletionOutcomeDoesNotOverwriteAlreadyCommittedSuccess()
    {
        var h = new Harness { ThrowAfterCommit = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Processor.ProcessPendingAsync(CancellationToken.None));
        Assert.Single(h.Completed);
        Assert.Empty(h.Failed);
        Assert.DoesNotContain(h.Logs, m => m.Contains("Scanned gespeichert"));
    }

    [Fact]
    public async Task FailedStatusWriteFailureStopsBatchInsteadOfClaimingSuccess()
    {
        var h = new Harness(2);
        h.OnStage = stage => { if (stage is "tickets" or "fail") throw new InvalidOperationException("failure"); };
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Processor.ProcessPendingAsync(CancellationToken.None));
        Assert.Single(h.Started);
        Assert.Empty(h.Completed);
        Assert.Empty(h.Failed);
    }

    [Fact]
    public async Task InvalidVerdictFromAdapterFailsBeforeTicketPlanning()
    {
        var h = new Harness { Verdict = (ScanVerdict)99 };
        await h.Processor.ProcessPendingAsync(CancellationToken.None);
        Assert.Single(h.Failed);
        Assert.DoesNotContain("tickets", h.Events);
    }

    [Fact]
    public async Task RecoveryResetsOnlyInterruptedScansAndAllowsThemToRunAgain()
    {
        var h = new Harness(4);
        h.States[h.Scans[0].Id] = "Scanning";
        h.States[h.Scans[2].Id] = "Scanned";
        h.States[h.Scans[3].Id] = "Failed";
        var recovery = new ScanRecovery(h, Microsoft.Extensions.Logging.Abstractions.NullLogger<ScanRecovery>.Instance);
        await recovery.RecoverAsync(CancellationToken.None);
        Assert.Equal(new[] { "Pending", "Pending", "Scanned", "Failed" }, h.Scans.Select(s => h.States[s.Id]));
        await h.Processor.ProcessPendingAsync(CancellationToken.None);
        Assert.Equal(2, h.Completed.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryPropagatesFailureOrCancellation(bool cancelled)
    {
        var h = new Harness();
        var recovery = new ScanRecovery(h, Microsoft.Extensions.Logging.Abstractions.NullLogger<ScanRecovery>.Instance);
        h.OnStage = _ => throw new InvalidOperationException("store-error");
        if (cancelled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recovery.RecoverAsync(new CancellationToken(true)));
            Assert.Empty(h.Events);
        }
        else await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.RecoverAsync(CancellationToken.None));
    }

    private sealed record TicketBatch : IPreparedScanTickets { public int Count => 1; }

    private sealed class Harness : IScanStore, IOsvClient, IFindingEnricher, IScanVerdictCalculator, IScanTicketPlanner, ILogger<ScanProcessor>
    {
        public FakeTimeProvider Clock { get; } = new();
        public List<PendingScan> Scans { get; } = [];
        public Dictionary<Guid, string> States { get; } = [];
        public List<Guid> Started { get; } = [];
        public Dictionary<Guid, ScanCompletion> Completed { get; } = [];
        public Dictionary<Guid, (string Message, DateTimeOffset At)> Failed { get; } = [];
        public List<string> Events { get; } = [];
        public List<string> Logs { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public Action<string>? OnStage { get; set; }
        public Guid? RejectStart { get; set; }
        public bool ThrowAfterCommit { get; set; }
        public int RequestedLimit { get; private set; }
        public DateTimeOffset PlannedAt { get; private set; }
        public ScanVerdict Verdict { get; set; } = ScanVerdict.NoGo;
        public IPreparedScanTickets TicketBatch { get; } = new TicketBatch();
        public IReadOnlyList<OsvFinding> Findings { get; set; }
        public IReadOnlyList<EnrichedFinding> Enriched { get; set; }
        public ScanProcessor Processor { get; }

        public Harness(int count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                var scan = new PendingScan(Guid.NewGuid(), Guid.NewGuid(), Clock.GetUtcNow().AddMinutes(i));
                Scans.Add(scan);
                States.Add(scan.Id, "Pending");
            }
            Findings = [new OsvFinding(Guid.NewGuid(), "TEST", null, OsvSeverity.High, "test")];
            Enriched = [new EnrichedFinding(Findings[0], true, 0.4)];
            Processor = new ScanProcessor(this, this, this, this, this, Clock, this);
        }
        private void Stage(string name, CancellationToken ct)
        {
            Events.Add(name);
            Tokens.Add(ct);
            ct.ThrowIfCancellationRequested();
            OnStage?.Invoke(name);
            ct.ThrowIfCancellationRequested();
        }
        public Task<IReadOnlyList<PendingScan>> GetPendingAsync(int limit, CancellationToken ct)
        {
            Stage("pending", ct);
            RequestedLimit = limit;
            return Task.FromResult<IReadOnlyList<PendingScan>>(Scans.Where(s => States[s.Id] == "Pending").ToArray());
        }
        public Task<bool> TryStartAsync(Guid id, CancellationToken ct)
        {
            Stage("start", ct);
            if (RejectStart == id || States[id] != "Pending") return Task.FromResult(false);
            States[id] = "Scanning";
            Started.Add(id);
            return Task.FromResult(true);
        }
        public Task<IReadOnlyList<OsvComponent>> GetComponentsAsync(Guid id, CancellationToken ct)
        {
            Stage("components", ct);
            Assert.Equal("Scanning", States[id]);
            return Task.FromResult<IReadOnlyList<OsvComponent>>([new(Findings.FirstOrDefault()?.ComponentId ?? Guid.NewGuid(), "pkg:nuget/Test@1.0.0")]);
        }
        public Task<IReadOnlyList<OsvFinding>> FindVulnerabilitiesAsync(IReadOnlyList<OsvComponent> components, CancellationToken ct)
        {
            Stage("osv", ct);
            return Task.FromResult(Findings);
        }
        public Task<IReadOnlyList<EnrichedFinding>> EnrichAsync(IReadOnlyList<OsvFinding> findings, CancellationToken ct)
        {
            Stage("enrich", ct);
            Assert.Same(Findings, findings);
            return Task.FromResult(Enriched);
        }
        public ScanVerdict Calculate(IReadOnlyList<EnrichedFinding> findings)
        {
            Events.Add("verdict");
            OnStage?.Invoke("verdict");
            Assert.Same(Enriched, findings);
            return Verdict;
        }
        public Task<IPreparedScanTickets> PrepareAsync(PendingScan scan, IReadOnlyList<EnrichedFinding> findings, DateTimeOffset now, CancellationToken ct)
        {
            Stage("tickets", ct);
            Assert.Same(Enriched, findings);
            PlannedAt = now;
            return Task.FromResult(TicketBatch);
        }
        public Task CompleteAsync(Guid id, ScanCompletion completion, CancellationToken ct)
        {
            Stage("complete", ct);
            States[id] = "Scanned";
            Completed.Add(id, completion);
            if (ThrowAfterCommit) throw new InvalidOperationException("response-lost-after-commit");
            return Task.CompletedTask;
        }
        public Task FailAsync(Guid id, string message, DateTimeOffset finishedAt, CancellationToken ct)
        {
            Stage("fail", ct);
            States[id] = "Failed";
            Failed.Add(id, (message, finishedAt));
            return Task.CompletedTask;
        }
        public Task<int> ResetInterruptedAsync(CancellationToken ct)
        {
            Stage("recovery", ct);
            var ids = States.Where(s => s.Value == "Scanning").Select(s => s.Key).ToArray();
            foreach (var id in ids) States[id] = "Pending";
            return Task.FromResult(ids.Length);
        }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Logs.Add(formatter(state, exception));
    }
}
