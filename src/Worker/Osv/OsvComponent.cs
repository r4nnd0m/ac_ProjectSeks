namespace Worker.Osv;

/// <summary>
/// Eingabe für OSV aus B1-Component.Id/Purl.
/// Die Version gehört bereits in die Purl, z.B. pkg:nuget/Newtonsoft.Json@12.0.1.
/// </summary>
public sealed record OsvComponent(Guid Id, string Purl);
