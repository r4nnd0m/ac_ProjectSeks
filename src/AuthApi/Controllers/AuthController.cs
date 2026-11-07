using AuthApi.Dtos;
using AuthApi.Models;
using AuthApi.Options;
using AuthApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _options;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IOptions<JwtOptions> options)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _options = options.Value;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        ApplicationUser? user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null || !user.IsActive || await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized();
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized();
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        IList<string> roles = await _userManager.GetRolesAsync(user);
        string role = roles.Single();
        string token = _tokenService.CreateAccessToken(user, role, false);

        return Ok(new LoginResponse(token, _options.ExpiresMinutes * 60, false, null));
    }
}