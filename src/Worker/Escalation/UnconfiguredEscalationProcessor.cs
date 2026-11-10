namespace Worker.Escalation;

/// <summary>Sichtbarer Platzhalter, bis die echten Team-Adapter angeschlossen sind.</summary>
internal sealed class UnconfiguredEscalationProcessor(ILogger<UnconfiguredEscalationProcessor> logger) : IEscalationProcessor
{
    public Task RunAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Ticketeskalation pausiert. Sie benötigt ConnectionStrings:Sims und die registrierte Folgeregel (C4).");
        return Task.CompletedTask;
    }
}
