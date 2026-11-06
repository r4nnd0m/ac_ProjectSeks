using Microsoft.Extensions.Options;

namespace Worker;

public class Worker(
    IServiceScopeFactory scopes,
    ILogger<Worker> logger,
    IOptions<WorkerOptions> options) : BackgroundService
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
                    "Testdurchlauf {Cycle}: ScanProcessor wird aufgerufen.",
                    ++cycle);
                await ProcessCycleAsync(stoppingToken);
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

    private async Task ProcessCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Ein eigener Arbeitsbereich pro Durchlauf: später auch für den DbContext.
            // AsyncScope räumt ebenso Dienste mit asynchronem Dispose zuverlässig auf.
            await using var scope = scopes.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<IScanProcessor>();
            await processor.ProcessPendingAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Ein gewünschtes Beenden ist kein fehlgeschlagener Scan-Durchlauf.
            throw;
        }
        catch (Exception exception)
        {
            // Auch die Konsolenausgabe soll keine fremden Fehlermeldungen mit Secrets enthalten.
            logger.LogError(
                "Scan-Durchlauf fehlgeschlagen ({ErrorType}). " +
                "Der nächste Durchlauf wird erneut versucht.",
                exception.GetType().Name);
        }
    }
}
