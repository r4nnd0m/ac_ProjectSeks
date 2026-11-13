namespace Worker;

public interface IScanProcessor
{
    Task ProcessPendingAsync(CancellationToken cancellationToken);
}
