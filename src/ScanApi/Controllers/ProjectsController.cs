using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanApi.Extensions;
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

        var created = await projectService.CreateAsync(User.GetTeamId(), request.Name.Trim(), User.GetUserId());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> GetById(Guid id)
    {
        var project = await projectService.GetAsync(id, User);
        return project is null ? NotFound() : Ok(project);
    }
}
