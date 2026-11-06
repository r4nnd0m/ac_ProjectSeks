namespace Worker.Escalation;

public sealed class EscalationJob(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<EscalationJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60), clock);
        logger.LogInformation("Eskalationsprüfung gestartet. Arbeitsabstand: 60 Sekunden.");
        try
        {
            do
            {
                stoppingToken.ThrowIfCancellationRequested();
                await RunCycleAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Angefordertes Beenden unterbricht die Arbeit oder das Warten.
        }
        finally { logger.LogInformation("Eskalationsprüfung sauber beendet."); }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IEscalationProcessor>().RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // Auch nach Fehlern Scope entsorgen; weder fremde Meldungen noch Secrets loggen.
            logger.LogError("Eskalationsdurchlauf fehlgeschlagen ({ErrorType}). Der nächste Durchlauf wird erneut versucht.", ex.GetType().Name);
        }
    }
}
