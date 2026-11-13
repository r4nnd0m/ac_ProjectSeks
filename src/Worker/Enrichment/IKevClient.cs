namespace Worker.Enrichment;

public interface IKevClient
{
    Task<HashSet<string>> GetKnownExploitedAsync(CancellationToken ct);
}
