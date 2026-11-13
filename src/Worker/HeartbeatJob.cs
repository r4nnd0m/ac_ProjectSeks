namespace Worker;

// Lebenszeichen im Log, unabhängig davon, ob gerade gescannt wird.
public sealed class HeartbeatJob(ILogger<HeartbeatJob> logger, TimeProvider clock) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        logger.LogInformation("Worker-Lebenszeichen aktiviert. Meldung alle 5 Minuten.");

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                stoppingToken.ThrowIfCancellationRequested();
                logger.LogInformation("Worker-Lebenszeichen: Anwendung läuft.");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
