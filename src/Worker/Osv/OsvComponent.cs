namespace Worker.Osv;

// Die Version steckt in der Purl, z.B. pkg:nuget/Newtonsoft.Json@12.0.1
public sealed record OsvComponent(Guid Id, string Purl);
