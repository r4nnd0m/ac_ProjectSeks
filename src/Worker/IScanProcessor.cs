namespace Worker;

/// <summary>Verarbeitet die wartenden Scan-Aufträge eines Worker-Durchlaufs.</summary>
public interface IScanProcessor
{
    Task ProcessPendingAsync(CancellationToken cancellationToken);
}
