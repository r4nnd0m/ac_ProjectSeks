using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScanApi.Models;
using ScanApi.Services;
using Sims.Shared.Data;
using Sims.Shared.Entities;
using Sims.Shared.Enums;

namespace ScanApi.Controllers;

[ApiController]
[Authorize]
public class ScansController(SimsDbContext db, ISbomParser parser) : ControllerBase
{
    [HttpPost("projects/{projectId:guid}/scans")]
    [Consumes("application/json")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Upload(Guid projectId, [FromQuery, Required, StringLength(50)] string version, [FromBody] CycloneDxBom bom)
    {
        var project = await db.Projects.FindAsync(projectId);
        if (project is null)
            return NotFound();

        var parsed = parser.Parse(bom);
        if (parsed.Errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(parsed.Errors.ToDictionary(err => "sbom", err => new[] { err })));
        }

        if (parsed.Components is null)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["sbom"] = ["Das SBOM konnte nicht verarbeitet werden."]
            }));
        }

        var scan = new Scan
        {
            ProjectId = projectId,
            Project = project,
            Version = version,
            Status = ScanStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            Components = parsed.Components.Select(c => new Component
            {
                Name = c.Name,
                Version = c.Version,
                Purl = c.Purl
            }).ToList()
        };

        db.Scans.Add(scan);
        await db.SaveChangesAsync();

        return Accepted(new ScanCreatedResponse(scan.Id, scan.Status.ToString()));
    }

    [HttpGet("projects/{projectId:guid}/scans")]
    public async Task<ActionResult<List<ScanSummaryResponse>>> List(Guid projectId)
    {
        var scans = await db.Scans
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(20)
            .Select(s => new ScanSummaryResponse(
                s.Id,
                s.Version,
                s.Status.ToString(),
                s.Verdict != null ? s.Verdict.ToString() : null,
                s.CreatedAt))
            .ToListAsync();

        return Ok(scans);
    }



















































































































































































































































































































































































































































































































































































































































































































































    [HttpGet("scans/{scanId:guid}")]
    public async Task<ActionResult<ScanDetailResponse>> GetById(Guid scanId)
    {
        var scan = await db.Scans
            .AsNoTracking()
            .Include(s => s.Findings)
            .ThenInclude(f => f.Component)
            .FirstOrDefaultAsync(s => s.Id == scanId);

        if (scan is null)
            return NotFound();

        var findings = scan.Findings
            .OrderByDescending(f => f.Severity)
            .Select(f => new FindingResponse(
                f.Id,
                f.Component.Name,
                f.Component.Version,
                f.VulnId,
                f.Cve,
                f.Severity.ToString(),
                f.IsKev,
                f.Epss))
            .ToList();

        return Ok(new ScanDetailResponse(
            scan.Id,
            scan.ProjectId,
            scan.Version,
            scan.Status.ToString(),
            scan.Verdict?.ToString(),
            scan.ErrorMessage,
            scan.CreatedAt,
            scan.FinishedAt,
            findings));
    }
}
