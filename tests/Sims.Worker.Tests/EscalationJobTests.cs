using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Worker.Escalation;

namespace Sims.Worker.Tests;

public sealed class EscalationJobTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task RunsImmediatelyThenEverySixtySecondsWithFreshDisposedScopes()
    {
        await using var app = new Harness((_, _) => Task.CompletedTask);
        await app.Job.StartAsync(CancellationToken.None);
        Assert.Equal(1, await app.NextDisposedAsync());
        app.Clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(1, app.StartedCount);
        app.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, await app.NextDisposedAsync());
        await app.StopAsync();
        Assert.Equal(new[] { "start:1", "dispose:1", "start:2", "dispose:2" }, app.Events);
        Assert.DoesNotContain(app.Logs.Entries, e => e.Level >= LogLevel.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErrorIsSanitizedAndNextCycleStillRuns(bool unrelatedCancellation)
    {
        await using var app = new Harness((id, _) => id == 1
            ? Task.FromException(unrelatedCancellation ? new OperationCanceledException("secret-detail") : new InvalidOperationException("secret-detail"))
            : Task.CompletedTask);
        await app.Job.StartAsync(CancellationToken.None);
        Assert.Equal(1, await app.NextDisposedAsync());
        app.Clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(2, await app.NextDisposedAsync());
        await app.StopAsync();
        var error = Assert.Single(app.Logs.Entries, e => e.Level == LogLevel.Error);
        Assert.DoesNotContain("secret-detail", error.Message);
        Assert.Contains(unrelatedCancellation ? "OperationCanceledException" : "InvalidOperationException", error.Message);
    }

    [Fact]
    public async Task LongRunningCycleNeverOverlapsTheNextOne()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app = new Harness((id, ct) => id == 1 ? release.Task.WaitAsync(ct) : Task.CompletedTask);
        await app.Job.StartAsync(CancellationToken.None);
        Assert.Equal(1, await app.NextStartedAsync());
        app.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, app.StartedCount);
        release.TrySetResult();
        Assert.Equal(1, await app.NextDisposedAsync());
        Assert.Equal(2, await app.NextDisposedAsync());
        await app.StopAsync();
        Assert.Equal(new[] { "start:1", "dispose:1", "start:2", "dispose:2" }, app.Events);
    }

    [Fact]
    public async Task ShutdownCancelsRunningWorkAndDisposesItWithoutAnError()
    {
        await using var app = new Harness((_, ct) => Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, ct));
        await app.Job.StartAsync(CancellationToken.None);
        Assert.Equal(1, await app.NextStartedAsync());
        await app.StopAsync();
        Assert.Equal(1, await app.NextDisposedAsync());
        Assert.DoesNotContain(app.Logs.Entries, e => e.Level >= LogLevel.Error);
        Assert.Contains(app.Logs.Entries, e => e.Message == "Eskalationsprüfung sauber beendet.");
    }

    [Fact]
    public async Task ShutdownInterruptsTimerAndPreventsFurtherCycles()
    {
        await using var app = new Harness((_, _) => Task.CompletedTask);
        await app.Job.StartAsync(CancellationToken.None);
        Assert.Equal(1, await app.NextDisposedAsync());
        await app.StopAsync();
        app.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(1, app.StartedCount);
        Assert.True(app.Job.ExecuteTask!.IsCompletedSuccessfully);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private int instances;
        private int startedCount;
        private readonly Channel<int> started = Channel.CreateUnbounded<int>();
        private readonly Channel<int> disposed = Channel.CreateUnbounded<int>();
        public FakeTimeProvider Clock { get; } = new();
        public ConcurrentQueue<string> Events { get; } = new();
        public LogSink Logs { get; } = new();
        public EscalationJob Job { get; }
        public int StartedCount => Volatile.Read(ref startedCount);

        public Harness(Func<int, CancellationToken, Task> run)
        {
            var collection = new ServiceCollection();
            collection.AddScoped<IEscalationProcessor>(_ => new Probe(Interlocked.Increment(ref instances), this, run));
            services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Job = new EscalationJob(services.GetRequiredService<IServiceScopeFactory>(), Clock, Logs);
        }
        public async Task<int> NextStartedAsync() => await started.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        public async Task<int> NextDisposedAsync() => await disposed.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        public async Task StopAsync()
        {
            using var stop = new CancellationTokenSource(Timeout);
            await Job.StopAsync(stop.Token);
        }
        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            Job.Dispose();
            await services.DisposeAsync();
        }
        private sealed class Probe(int id, Harness owner, Func<int, CancellationToken, Task> run) : IEscalationProcessor, IAsyncDisposable
        {
            public Task RunAsync(CancellationToken ct)
            {
                Interlocked.Increment(ref owner.startedCount);
                owner.Events.Enqueue($"start:{id}");
                owner.started.Writer.TryWrite(id);
                return run(id, ct);
            }
            public ValueTask DisposeAsync()
            {
                owner.Events.Enqueue($"dispose:{id}");
                owner.disposed.Writer.TryWrite(id);
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class LogSink : ILogger<EscalationJob>
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue((level, formatter(state, exception)));
    }
}
