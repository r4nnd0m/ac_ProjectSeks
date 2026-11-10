namespace Worker.Escalation;

public interface IEscalationProcessor
{
    Task RunAsync(CancellationToken ct);
}
