namespace Worker.Escalation;

internal sealed class UnconfiguredEscalationProcessor(ILogger<UnconfiguredEscalationProcessor> logger) : IEscalationProcessor
{
    public Task RunAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Ticketeskalation pausiert. Sie benötigt ConnectionStrings:Sims und die Eskalationsregeln.");
        return Task.CompletedTask;
    }
}
