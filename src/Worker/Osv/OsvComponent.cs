namespace Worker.Osv;

/// <summary>
/// Eingabe für OSV, keine Datenbank-Entity. Später aus B1-Component.Id/Purl abbilden.
/// Die Version gehört bereits in die Purl, z.B. pkg:nuget/Newtonsoft.Json@12.0.1.
/// </summary>
public sealed record OsvComponent(Guid Id, string Purl);
