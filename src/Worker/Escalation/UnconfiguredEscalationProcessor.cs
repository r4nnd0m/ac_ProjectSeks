namespace Worker.Escalation;

/// <summary>Sichtbarer Platzhalter, bis die echten Team-Adapter angeschlossen sind.</summary>
internal sealed class UnconfiguredEscalationProcessor(ILogger<UnconfiguredEscalationProcessor> logger) : IEscalationProcessor
{
    public Task RunAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        logger.LogInformation(
            "D4-Platzhalter aktiv: Ticket-Datenbank, Eskalationsregeln und Audit noch nicht angeschlossen. Keine Tickets verändert.");
        return Task.CompletedTask;
    }
}
