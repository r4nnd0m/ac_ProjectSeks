using Sims.Shared.Enums;

namespace Sims.Shared.Entities;

public class Scan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Version { get; set; } = string.Empty;
    public ScanStatus Status { get; set; } = ScanStatus.Pending;
    public Verdict? Verdict { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public List<Component> Components { get; set; } = [];
    public List<Finding> Findings { get; set; } = [];
}
