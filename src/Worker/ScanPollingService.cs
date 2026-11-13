using Microsoft.Extensions.Options;
using Worker.Scanning;

namespace Worker;

public sealed class ScanPollingService(
    IServiceScopeFactory scopes,
    ILogger<ScanPollingService> logger,
    IOptions<WorkerOptions> options,
    TimeProvider clock) : BackgroundService
{
    private bool recoveryCompleted;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = options.Value.PollIntervalSeconds;
        logger.LogInformation(
            "Worker gestartet. Arbeitsabstand: {PollIntervalSeconds} Sekunden.",
            intervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds), clock);
        long cycle = 0;

        try
        {
            do
            {
                stoppingToken.ThrowIfCancellationRequested();
                logger.LogInformation(
                    "Worker-Durchlauf {Cycle}: Neustartprüfung bzw. Scan-Verarbeitung wird aufgerufen.",
                    ++cycle);
                await ProcessCycleAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            logger.LogInformation("Worker sauber beendet.");
        }
    }

    private async Task ProcessCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Neuer Scope pro Durchlauf, damit jeder Durchlauf einen frischen DbContext bekommt.
            await using var scope = scopes.CreateAsyncScope();
            if (!recoveryCompleted)
            {
                await scope.ServiceProvider.GetRequiredService<IScanRecovery>().RecoverAsync(stoppingToken);
                recoveryCompleted = true;
            }
            var processor = scope.ServiceProvider.GetRequiredService<IScanProcessor>();
            await processor.ProcessPendingAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Scan-Durchlauf fehlgeschlagen ({ErrorType}). " +
                "Der nächste Durchlauf wird erneut versucht.",
                exception.GetType().Name);
        }
    }
}
