using Microsoft.Extensions.Options;

namespace Worker;

public class Worker(ILogger<Worker> logger, IOptions<WorkerOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = options.Value.PollIntervalSeconds;
        logger.LogInformation(
            "Worker gestartet. Arbeitsabstand: {PollIntervalSeconds} Sekunden.",
            intervalSeconds);
        logger.LogInformation(
            "Grundgerüst aktiv: Scan-Verarbeitung und Datenbankanbindung sind noch nicht angeschlossen.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        long cycle = 0;

        try
        {
            // Der erste Testdurchlauf startet sofort, weitere folgen im eingestellten Abstand.
            do
            {
                stoppingToken.ThrowIfCancellationRequested();
                logger.LogInformation(
                    "Testdurchlauf {Cycle}: Zeitsteuerung aktiv; noch keine Scan-Verarbeitung.",
                    ++cycle);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Strg+C beendet auch das Warten auf den nächsten Durchlauf.
        }
        finally
        {
            logger.LogInformation("Worker sauber beendet.");
        }
    }
}
