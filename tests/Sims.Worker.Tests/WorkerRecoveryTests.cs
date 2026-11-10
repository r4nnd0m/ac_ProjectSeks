using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Worker;
using Worker.Scanning;

namespace Sims.Worker.Tests;

public sealed class WorkerRecoveryTests
{
    [Fact]
    public async Task RecoveryRunsBeforeFirstScanAndNeverAgainAfterSuccess()
    {
        await using var h = new Harness();
        await h.Worker.StartAsync(CancellationToken.None);
        Assert.Equal(1, await h.NextDisposedAsync());
        Assert.Equal(new[] { "recover:1", "process:1", "dispose:1" }, h.Events);
        h.Clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(1, h.ProcessCount);
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, await h.NextDisposedAsync());
        Assert.Equal(1, h.RecoveryCount);
        Assert.Equal(2, h.ProcessCount);
        Assert.Equal(new[] { "recover:1", "process:1", "dispose:1", "process:2", "dispose:2" }, h.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRecoveryBlocksScanningAndRetriesInFreshScope(bool unrequestedCancellation)
    {
        await using var h = new Harness((attempt, _) => attempt == 1
            ? Task.FromException(unrequestedCancellation ? new OperationCanceledException("secret-detail") : new InvalidOperationException("secret-detail"))
            : Task.CompletedTask);
        await h.Worker.StartAsync(CancellationToken.None);
        Assert.Equal(1, await h.NextDisposedAsync());
        Assert.Equal(0, h.ProcessCount);
        h.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(2, await h.NextDisposedAsync());
        Assert.Equal(2, h.RecoveryCount);
        Assert.Equal(1, h.ProcessCount);
        Assert.Single(h.Errors);
        Assert.DoesNotContain(h.Errors, message => message.Contains("secret-detail"));
        h.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(3, await h.NextDisposedAsync());
        Assert.Equal(2, h.RecoveryCount);
    }

    [Fact]
    public async Task CancellationDuringRecoveryStopsWithoutStartingAnyScan()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var h = new Harness(async (_, ct) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        });
        await h.Worker.StartAsync(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await h.StopAsync();
        Assert.Equal(1, await h.NextDisposedAsync());
        Assert.Equal(0, h.ProcessCount);
        Assert.Empty(h.Errors);
        Assert.True(h.Worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ScanFailureDoesNotResetInterruptedWorkAgainOnTheNextTick()
    {
        await using var h = new Harness(process: (attempt, _) => attempt == 1
            ? Task.FromException(new InvalidOperationException("failure")) : Task.CompletedTask);
        await h.Worker.StartAsync(CancellationToken.None);
        Assert.Equal(1, await h.NextDisposedAsync());
        h.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(2, await h.NextDisposedAsync());
        Assert.Equal(1, h.RecoveryCount);
        Assert.Equal(2, h.ProcessCount);
    }

    private sealed class Harness : IAsyncDisposable, ILogger<global::Worker.Worker>
    {
        private readonly ServiceProvider services;
        private readonly Channel<int> disposed = Channel.CreateUnbounded<int>();
        private int scopes;
        private int recoveryCount;
        private int processCount;
        public FakeTimeProvider Clock { get; } = new();
        public ConcurrentQueue<string> Events { get; } = new();
        public ConcurrentQueue<string> Errors { get; } = new();
        public int RecoveryCount => Volatile.Read(ref recoveryCount);
        public int ProcessCount => Volatile.Read(ref processCount);
        public global::Worker.Worker Worker { get; }

        public Harness(Func<int, CancellationToken, Task>? recover = null, Func<int, CancellationToken, Task>? process = null)
        {
            var collection = new ServiceCollection();
            collection.AddScoped(_ => new Probe(Interlocked.Increment(ref scopes), this,
                recover ?? ((_, _) => Task.CompletedTask), process ?? ((_, _) => Task.CompletedTask)));
            collection.AddScoped<IScanRecovery>(s => s.GetRequiredService<Probe>());
            collection.AddScoped<IScanProcessor>(s => s.GetRequiredService<Probe>());
            services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            Worker = new global::Worker.Worker(services.GetRequiredService<IServiceScopeFactory>(), this,
                Options.Create(new WorkerOptions { PollIntervalSeconds = 30 }), Clock);
        }
        public async Task<int> NextDisposedAsync() => await disposed.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        public async Task StopAsync()
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await Worker.StopAsync(stop.Token);
        }
        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            Worker.Dispose();
            await services.DisposeAsync();
        }
        private sealed class Probe(int id, Harness owner, Func<int, CancellationToken, Task> recover,
            Func<int, CancellationToken, Task> process) : IScanRecovery, IScanProcessor, IAsyncDisposable
        {
            private bool disposed;
            public Task RecoverAsync(CancellationToken ct)
            {
                owner.Events.Enqueue($"recover:{id}");
                return recover(Interlocked.Increment(ref owner.recoveryCount), ct);
            }
            public Task ProcessPendingAsync(CancellationToken ct)
            {
                owner.Events.Enqueue($"process:{id}");
                return process(Interlocked.Increment(ref owner.processCount), ct);
            }
            public ValueTask DisposeAsync()
            {
                // Mehrere DI-Verträge zeigen auf dieselbe scoped Instanz.
                if (!disposed)
                {
                    disposed = true;
                    owner.Events.Enqueue($"dispose:{id}");
                    owner.disposed.Writer.TryWrite(id);
                }
                return ValueTask.CompletedTask;
            }
        }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (level >= LogLevel.Error) Errors.Enqueue(formatter(state, exception));
        }
    }
}
