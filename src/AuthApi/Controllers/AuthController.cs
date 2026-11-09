using System.Security.Claims;
using AuthApi.Dtos;
using AuthApi.Models;
using AuthApi.Options;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using Microsoft.Extensions.Options;
using Sims.Contracts;

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

        if (user.TwoFactorEnabled)
        {
            string mfaToken = _tokenService.CreateMfaToken(user);
            return Ok(new LoginResponse(null, null, true, mfaToken));
        }
        
        await _userManager.ResetAccessFailedCountAsync(user);

        IList<string> roles = await _userManager.GetRolesAsync(user);
        string role = roles.Single();
        string token = _tokenService.CreateAccessToken(user, role, false);

        return Ok(new LoginResponse(token, _options.ExpiresMinutes * 60, false, null));
    }
    

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        string? id = User.FindFirstValue(ClaimNames.Sub);
        if (id == null)
        {
            return Unauthorized();
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(id);
        if (user == null || !user.IsActive)
        {
            return Unauthorized();
        }

        IList<string> roles = await _userManager.GetRolesAsync(user);
        string role = roles.Single();

        return Ok(new MeResponse(user.Id,user.Email!, role, user.TeamId,user.TwoFactorEnabled));
    }
    
}