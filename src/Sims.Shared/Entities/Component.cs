namespace Sims.Shared.Entities;

public class Component
{
    public const int MaxNameLength = 200;
    public const int MaxVersionLength = 100;
    public const int MaxPurlLength = 500;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScanId { get; set; }
    public Scan Scan { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Purl { get; set; } = string.Empty;

    public List<Finding> Findings { get; set; } = [];
}
