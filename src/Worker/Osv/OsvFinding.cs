namespace Worker.Osv;

public sealed record OsvFinding(
    Guid ComponentId, string VulnId, string? Cve, OsvSeverity Severity, string? Summary);

public enum OsvSeverity { Low, Medium, High, Critical }
