using AuthApi.Dtos;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sims.Contracts;

namespace AuthApi.Controllers;

[ApiController]
[Route("teams")]
[Authorize(Policy = Policies.AdminOnly)]
public class TeamsController : ControllerBase
{
    private readonly TeamService _teamService;

    public TeamsController(TeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        List<TeamResponse> teams = await _teamService.ListAsync();
        return Ok(teams);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTeamRequest request)
    {
        TeamResponse? team = await _teamService.CreateAsync(request);
        if (team == null)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Ein Team mit diesem Namen gibt es schon.");
        }

        return StatusCode(StatusCodes.Status201Created, team);
    }
}