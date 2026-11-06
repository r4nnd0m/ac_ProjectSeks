namespace Worker;

/// <summary>
/// Regelmäßiges Lebenszeichen der Anwendung, unabhängig von der Scan-Dauer.
/// Eine Meldung bestätigt keinen erfolgreichen Scan.
/// </summary>
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
            // Das Stoppsignal beendet auch das Warten auf den nächsten Zeitpunkt.
        }
    }
}
