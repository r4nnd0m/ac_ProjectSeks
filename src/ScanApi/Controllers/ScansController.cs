using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScanApi.Extensions;
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
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid projectId, [FromQuery, Required, StringLength(50)] string version, [FromBody] CycloneDxBom bom)
    {
        var project = await db.Projects.ForUser(User).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null)
            return NotFound();

        var (components, errors) = parser.Parse(bom);
        if (components is null)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["sbom"] = errors.ToArray()
            }));
        }

        var scan = new Scan
        {
            ProjectId = projectId,
            Project = project,
            Version = version.Trim(),
            Status = ScanStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            Components = components
        };

        db.Scans.Add(scan);
        await db.SaveChangesAsync();

        return Accepted(new ScanCreatedResponse(scan.Id, scan.Status.ToString()));
    }

    [HttpGet("projects/{projectId:guid}/scans")]
    public async Task<ActionResult<List<ScanSummaryResponse>>> List(Guid projectId)
    {
        if (!await db.Projects.ForUser(User).AnyAsync(p => p.Id == projectId))
            return NotFound();

        var scans = await db.Scans
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id)
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
        var projectIds = db.Projects.ForUser(User).Select(p => p.Id);
        var scan = await db.Scans
            .AsNoTracking()
            .Include(s => s.Findings)
            .ThenInclude(f => f.Component)
            .FirstOrDefaultAsync(s => s.Id == scanId && projectIds.Contains(s.ProjectId));

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
