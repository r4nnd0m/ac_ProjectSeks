namespace Worker.Osv;

/// <summary>Ergebnis der externen Abfrage. Erzeugt noch keinen Datenbankeintrag.</summary>
public sealed record OsvFinding(
    Guid ComponentId, string VulnId, string? Cve, OsvSeverity Severity, string? Summary);

/// <summary>
/// Worker-lokaler Schweregrad, solange der gemeinsame B1-Typ fehlt.
/// Bei der Integration ausdrücklich nach Namen auf B1-Severity abbilden, nicht nach Zahlen.
/// </summary>
public enum OsvSeverity { Low, Medium, High, Critical }
