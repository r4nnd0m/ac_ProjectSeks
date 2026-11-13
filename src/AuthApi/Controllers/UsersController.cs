using System.Security.Claims;
using AuthApi.Dtos;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sims.Contracts;

namespace AuthApi.Controllers;

[ApiController]
[Route("users")]
[Authorize(Policy = Policies.AdminOnly)]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        List<UserResponse> users = await _userService.ListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        UserResult result = await _userService.CreateAsync(request);
        if (!result.Succeeded)
        {
            ProblemDetails problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Der User konnte nicht angelegt werden."
            };
            problem.Extensions["errors"] = result.Errors;
            return BadRequest(problem);
        }

        return StatusCode(StatusCodes.Status201Created, result.User);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        Guid currentUserId = Guid.Parse(User.FindFirstValue(ClaimNames.Sub)!);
        DeactivateOutcome outcome = await _userService.DeactivateAsync(id, currentUserId);

        if (outcome == DeactivateOutcome.NotFound)
        {
            return NotFound();
        }

        if (outcome == DeactivateOutcome.CannotDeactivateSelf)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Der eigene Account kann nicht deaktiviert werden.");
        }

        if (outcome == DeactivateOutcome.UpdateFailed)
            return Conflict();

        return NoContent();
    }
}