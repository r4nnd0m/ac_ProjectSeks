using System.Security.Claims;
using AuthApi.Dtos;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Sims.Contracts;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/mfa")]
public class MfaController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public MfaController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [Authorize]
    [HttpPost("setup")]
    public async Task<IActionResult> Setup()
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

        if (user.TwoFactorEnabled)
        {
            return Conflict();
        }

        await _userManager.ResetAuthenticatorKeyAsync(user);
        string? secret = await _userManager.GetAuthenticatorKeyAsync(user);

        if (secret == null)
        {
            return StatusCode(500);
        }

        string uri = $"otpauth://totp/SIMS:{Uri.EscapeDataString(user.Email!)}?secret={secret}&issuer=SIMS&digits=6";
        return Ok(new MfaSetupResponse(secret, uri));
    }

    [Authorize]
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(MfaConfirmRequest request)
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

        if (user.TwoFactorEnabled)
        {
            return Conflict();
        }

        bool valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            request.Code);

        if (!valid)
        {
            return BadRequest();
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        return NoContent();
    }
}