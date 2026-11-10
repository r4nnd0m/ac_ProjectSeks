using Sims.Shared.Enums;

namespace Sims.Shared.Entities;

public class Finding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScanId { get; set; }
    public Scan Scan { get; set; } = null!;
    public Guid ComponentId { get; set; }
    public Component Component { get; set; } = null!;
    public string VulnId { get; set; } = string.Empty;
    public string? Cve { get; set; }
    public Severity Severity { get; set; }
    public bool IsKev { get; set; }
    public double? Epss { get; set; }
    public string? Summary { get; set; }
}
