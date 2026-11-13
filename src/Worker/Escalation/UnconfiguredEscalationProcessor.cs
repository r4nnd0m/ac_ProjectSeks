namespace Worker.Escalation;

// Läuft, wenn keine Datenbank konfiguriert ist.
internal sealed class UnconfiguredEscalationProcessor(ILogger<UnconfiguredEscalationProcessor> logger) : IEscalationProcessor
{
    public Task RunAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Ticketeskalation pausiert, ConnectionStrings:Sims ist nicht gesetzt.");
        return Task.CompletedTask;
    }
}
