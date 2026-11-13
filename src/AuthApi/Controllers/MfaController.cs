using System.Security.Claims;
using System.Text;
using AuthApi.Dtos;
using AuthApi.Models;
using AuthApi.Options;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Sims.Contracts;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/mfa")]
public class MfaController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _options;

    public MfaController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IOptions<JwtOptions> options)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _options = options.Value;
    }

    [Authorize]
    [HttpPost("setup")]
    public async Task<IActionResult> Setup()
    {
        ApplicationUser? user = await FindSignedInUserAsync();
        if (user == null || !user.IsActive)
        {
            return Unauthorized();
        }

        if (user.TwoFactorEnabled)
        {
            return Conflict();
        }

        var reset = await _userManager.ResetAuthenticatorKeyAsync(user);
        if (!reset.Succeeded)
            return Conflict();
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
        ApplicationUser? user = await FindSignedInUserAsync();
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

        var enabled = await _userManager.SetTwoFactorEnabledAsync(user, true);
        if (!enabled.Succeeded)
            return Conflict();
        return NoContent();
    }

    private Task<ApplicationUser?> FindSignedInUserAsync()
    {
        var id = User.FindFirstValue(ClaimNames.Sub);
        return id is null ? Task.FromResult<ApplicationUser?>(null) : _userManager.FindByIdAsync(id);
    }

    [EnableRateLimiting("login")]
    [HttpPost("verify")]
    public async Task<IActionResult> Verify(MfaVerifyRequest request)
    {
        TokenValidationParameters parameters = new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = MfaTokenConstants.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            ClockSkew = AccessTokenPolicy.ClockSkew
        };
        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(request.MfaToken, parameters);
        if (!result.IsValid)
        {
            return Unauthorized();
        }

        string? purpose = result.ClaimsIdentity.FindFirst(MfaTokenConstants.PurposeClaim)?.Value;
        if (purpose != MfaTokenConstants.PurposeValue)
        {
            return Unauthorized();
        }

        string? id = result.ClaimsIdentity.FindFirst(ClaimNames.Sub)?.Value;
        if (id == null)
        {
            return Unauthorized();
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(id);
        if (user == null || !user.IsActive || !user.TwoFactorEnabled || await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized();
        }

        bool valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            request.Code);

        if (!valid)
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized();
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        IList<string> roles = await _userManager.GetRolesAsync(user);
        if (roles.Count != 1 || !Roles.All.Contains(roles[0]))
            return Unauthorized();

        string role = roles[0];
        string token = _tokenService.CreateAccessToken(user, role, true);
        return Ok(new LoginResponse(token, _options.ExpiresMinutes * 60, false, null));
    }
}