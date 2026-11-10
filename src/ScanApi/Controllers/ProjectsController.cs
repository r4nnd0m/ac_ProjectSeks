using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sims.Contracts;
using ScanApi.Models;
using ScanApi.Services;

namespace ScanApi.Controllers;

[ApiController]
[Route("projects")]
[Authorize]
public class ProjectsController(IProjectService projectService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProjectResponse>>> GetAll()
    {
        var items = await projectService.ListAsync(User);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> Create([FromBody] CreateProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ValidationProblem();

        var teamId = Guid.Parse(User.FindFirstValue(ClaimNames.TeamId)!);
        var ownerId = Guid.Parse(User.FindFirstValue(ClaimNames.Sub)!);

        var created = await projectService.CreateAsync(teamId, request.Name.Trim(), ownerId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> GetById(Guid id)
    {
        var project = await projectService.GetAsync(id, User);
        return project is null ? NotFound() : Ok(project);
    }
}
