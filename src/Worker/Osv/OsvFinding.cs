namespace Worker.Osv;

/// <summary>Ergebnis der externen Abfrage. Erzeugt noch keinen Datenbankeintrag.</summary>
public sealed record OsvFinding(
    Guid ComponentId, string VulnId, string? Cve, OsvSeverity Severity, string? Summary);

/// <summary>
/// Schweregrad der OSV-Antwort. Der SQL-Adapter ordnet ihn der B1-Severity zu.
/// </summary>
public enum OsvSeverity { Low, Medium, High, Critical }
