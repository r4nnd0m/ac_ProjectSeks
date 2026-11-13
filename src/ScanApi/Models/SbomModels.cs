namespace ScanApi.Models;

public record CycloneDxBom(string? BomFormat, string? SpecVersion, List<CycloneDxComponent?>? Components);

public record CycloneDxComponent(string? Name, string? Version, string? Purl);

public record ScanCreatedResponse(Guid ScanId, string Status);

public record ScanSummaryResponse(Guid Id, string Version, string Status, string? Verdict, DateTimeOffset CreatedAt);

public record FindingResponse(Guid Id, string Component, string ComponentVersion, string VulnId, string? Cve, string Severity, bool IsKev, double? Epss);

public record ScanDetailResponse(Guid Id, Guid ProjectId, string Version, string Status, string? Verdict, string? ErrorMessage, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt, List<FindingResponse> Findings);
