using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Worker;
using Worker.Scanning;

namespace Sims.Worker.Tests;

public sealed class WorkerLifecycleTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(8);

    [Fact]
    public async Task CreatesAndDisposesFreshProcessorForEachCycle()
    {
        await using var app = new WorkerHarness((_, _) => Task.CompletedTask);
        await app.StartAsync();
        await app.State.SecondDisposed.Task.WaitAsync(TestTimeout);
        await app.StopAsync();

        Assert.Equal(new[] { 1, 2 }, app.State.Started.Take(2));
        Assert.Equal(app.State.Started.ToArray(), app.State.Disposed.ToArray());
        Assert.DoesNotContain(app.Logs.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedCycleIsDisposedAndTheNextCycleStillRuns(bool unrelatedCancellation)
    {
        await using var app = new WorkerHarness((id, _) => id == 1
            ? Task.FromException(unrelatedCancellation
                ? new OperationCanceledException("sensitive-detail")
                : new InvalidOperationException("sensitive-detail"))
            : Task.CompletedTask);
        await app.StartAsync();
        await app.State.SecondDisposed.Task.WaitAsync(TestTimeout);
        await app.StopAsync();

        Assert.Equal(new[] { 1, 2 }, app.State.Started.Take(2));
        Assert.Equal(app.State.Started.ToArray(), app.State.Disposed.ToArray());
        var error = Assert.Single(app.Logs.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains(unrelatedCancellation
            ? nameof(OperationCanceledException)
            : nameof(InvalidOperationException), error.Message);
        Assert.DoesNotContain("sensitive-detail", error.Message);
    }

    [Fact]
    public async Task ShutdownCancelsInFlightWorkAndDisposesItsScopeWithoutAnError()
    {
        await using var app = new WorkerHarness(
            (_, cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken),
            pollIntervalSeconds: 30);
        await app.StartAsync();
        await app.State.FirstStarted.Task.WaitAsync(TestTimeout);
        await app.StopAsync();

        Assert.Equal(new[] { 1 }, app.State.Started.ToArray());
        Assert.Equal(new[] { 1 }, app.State.Disposed.ToArray());
        Assert.DoesNotContain(app.Logs.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Contains(app.Logs.Entries, entry => entry.Message == "Worker sauber beendet.");
    }

    [Fact]
    public async Task ShutdownInterruptsTheThirtySecondWait()
    {
        await using var app = new WorkerHarness((_, _) => Task.CompletedTask, pollIntervalSeconds: 30);
        await app.StartAsync();
        await app.State.FirstDisposed.Task.WaitAsync(TestTimeout);
        await app.StopAsync();

        Assert.Equal(new[] { 1 }, app.State.Started.ToArray());
        Assert.DoesNotContain(app.Logs.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Contains(app.Logs.Entries, entry => entry.Message == "Worker sauber beendet.");
    }

    [Fact]
    public async Task SlowCycleDoesNotOverlapTheNextCycle()
    {
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app = new WorkerHarness((id, cancellationToken) => id == 1
            ? releaseFirst.Task.WaitAsync(cancellationToken)
            : Task.CompletedTask);
        await app.StartAsync();
        await app.State.FirstStarted.Task.WaitAsync(TestTimeout);

        // Der Timer tickt nach einer Sekunde, während der erste Aufruf noch wartet.
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        Assert.Equal(new[] { 1 }, app.State.Started.ToArray());
        Assert.Empty(app.State.Disposed);

        releaseFirst.SetResult();
        await app.State.SecondDisposed.Task.WaitAsync(TestTimeout);
        await app.StopAsync();

        Assert.Equal(new[] { "start:1", "dispose:1", "start:2", "dispose:2" },
            app.State.Events.Take(4));
    }

    private sealed class WorkerHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly global::Worker.Worker _worker;

        public ProbeState State { get; }
        public RecordingLoggerProvider Logs { get; } = new();

        public WorkerHarness(Func<int, CancellationToken, Task> action, int pollIntervalSeconds = 1)
        {
            State = new ProbeState(action);
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.ClearProviders().AddProvider(Logs));
            services.AddSingleton(State);
            services.AddScoped<IScanProcessor, ProbeProcessor>();
            services.AddScoped<IScanRecovery, EmptyRecovery>();
            _services = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
            _worker = new global::Worker.Worker(
                _services.GetRequiredService<IServiceScopeFactory>(),
                _services.GetRequiredService<ILogger<global::Worker.Worker>>(),
                Options.Create(new WorkerOptions { PollIntervalSeconds = pollIntervalSeconds }), TimeProvider.System);
        }

        public Task StartAsync() => _worker.StartAsync(CancellationToken.None);

        public async Task StopAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _worker.StopAsync(timeout.Token);
            Assert.True(_worker.ExecuteTask?.IsCompletedSuccessfully,
                "Der Worker muss innerhalb von drei Sekunden sauber beendet werden.");
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await StopAsync();
            }
            finally
            {
                _worker.Dispose();
                await _services.DisposeAsync();
            }
        }
    }

    private sealed class EmptyRecovery : IScanRecovery
    {
        public Task RecoverAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ProbeState(Func<int, CancellationToken, Task> action)
    {
        private int _nextId;
        public ConcurrentQueue<int> Started { get; } = new();
        public ConcurrentQueue<int> Disposed { get; } = new();
        public ConcurrentQueue<string> Events { get; } = new();
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int NextId() => Interlocked.Increment(ref _nextId);

        public Task ProcessAsync(int id, CancellationToken cancellationToken)
        {
            Started.Enqueue(id);
            Events.Enqueue($"start:{id}");
            FirstStarted.TrySetResult();
            return action(id, cancellationToken);
        }

        public void RecordDisposal(int id)
        {
            Events.Enqueue($"dispose:{id}");
            Disposed.Enqueue(id);
            FirstDisposed.TrySetResult();
            if (Disposed.Count >= 2)
                SecondDisposed.TrySetResult();
        }
    }

    private sealed class ProbeProcessor(ProbeState state) : IScanProcessor, IAsyncDisposable
    {
        private readonly int _id = state.NextId();

        public Task ProcessPendingAsync(CancellationToken cancellationToken) =>
            state.ProcessAsync(_id, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            // Der Arbeitsbereich muss auch asynchrones Aufräumen abwarten.
            await Task.Yield();
            state.RecordDisposal(_id);
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Entries);
        public void Dispose() { }
    }

    private sealed class RecordingLogger(ConcurrentQueue<(LogLevel Level, string Message)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
