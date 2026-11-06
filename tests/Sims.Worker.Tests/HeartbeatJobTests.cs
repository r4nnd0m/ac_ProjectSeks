using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Worker;

namespace Sims.Worker.Tests;

public sealed class HeartbeatJobTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(3);
    private const string HeartbeatMessage = "Worker-Lebenszeichen: Anwendung läuft.";

    [Fact]
    public async Task EmitsFirstHeartbeatAfterFiveMinutesThenRepeats()
    {
        var clock = new FakeTimeProvider();
        var logger = new RecordingLogger();
        using var job = new HeartbeatJob(logger, clock);
        await job.StartAsync(CancellationToken.None);

        try
        {
            await AssertStartedAsync(logger);
            clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
            Assert.False(logger.Messages.Reader.TryRead(out _));

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(HeartbeatMessage, await ReadMessageAsync(logger));

            clock.Advance(TimeSpan.FromMinutes(5));
            Assert.Equal(HeartbeatMessage, await ReadMessageAsync(logger));
            Assert.False(logger.Messages.Reader.TryRead(out _));
        }
        finally
        {
            await StopAsync(job);
        }
    }

    [Fact]
    public async Task ShutdownInterruptsTheWaitAndPreventsFurtherHeartbeats()
    {
        var clock = new FakeTimeProvider();
        var logger = new RecordingLogger();
        using var job = new HeartbeatJob(logger, clock);
        await job.StartAsync(CancellationToken.None);

        try
        {
            await AssertStartedAsync(logger);
            await StopAsync(job);
            clock.Advance(TimeSpan.FromHours(1));
            Assert.False(logger.Messages.Reader.TryRead(out _));
        }
        finally
        {
            await StopAsync(job);
        }
    }

    [Fact]
    public async Task HeartbeatContinuesWhileScanProcessingIsWaiting()
    {
        var clock = new FakeTimeProvider();
        var logger = new RecordingLogger();
        var processor = new WaitingProcessor();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new HeartbeatLoggerProvider(logger));
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddOptions<WorkerOptions>()
            .Configure(options => options.PollIntervalSeconds = 86400);
        builder.Services.AddScoped<IScanProcessor>(_ => processor);
        builder.Services.AddHostedService<global::Worker.Worker>();
        builder.Services.AddHostedService<HeartbeatJob>();
        using var host = builder.Build();
        await host.StartAsync();

        try
        {
            await processor.Started.Task.WaitAsync(TestTimeout);
            await AssertStartedAsync(logger);
            clock.Advance(TimeSpan.FromMinutes(5));

            Assert.Equal(HeartbeatMessage, await ReadMessageAsync(logger));
            Assert.False(processor.Stopped.Task.IsCompleted);
        }
        finally
        {
            using var timeout = new CancellationTokenSource(TestTimeout);
            await host.StopAsync(timeout.Token);
        }

        Assert.True(processor.Stopped.Task.IsCompletedSuccessfully);
    }

    private static async Task AssertStartedAsync(RecordingLogger logger) =>
        Assert.Equal("Worker-Lebenszeichen aktiviert. Meldung alle 5 Minuten.",
            await ReadMessageAsync(logger));

    private static Task<string> ReadMessageAsync(RecordingLogger logger) =>
        logger.Messages.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout);

    private static async Task StopAsync(HeartbeatJob job)
    {
        using var timeout = new CancellationTokenSource(TestTimeout);
        await job.StopAsync(timeout.Token);
        Assert.True(job.ExecuteTask?.IsCompletedSuccessfully);
    }

    private sealed class WaitingProcessor : IScanProcessor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ProcessPendingAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                Stopped.TrySetResult();
            }
        }
    }

    private sealed class HeartbeatLoggerProvider(RecordingLogger logger) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) =>
            categoryName == typeof(HeartbeatJob).FullName ? logger : NullLogger.Instance;
        public void Dispose() { }
    }

    private sealed class RecordingLogger : ILogger<HeartbeatJob>
    {
        public Channel<string> Messages { get; } = Channel.CreateUnbounded<string>();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Writer.TryWrite(formatter(state, exception));
    }
}
