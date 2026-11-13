using System.Security.Claims;
using System.Text;
using AuthApi.Models;
using AuthApi.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Sims.Contracts;

namespace AuthApi.Services;

public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string CreateAccessToken(ApplicationUser user, string role, bool mfaPassed)
    {
        List<Claim> claims = new List<Claim>
        {
            new Claim(ClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimNames.Name, user.Email!),
            new Claim(ClaimNames.Role, role),
            new Claim(ClaimNames.TeamId, user.TeamId.ToString()),
            new Claim(ClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (mfaPassed)
        {
            claims.Add(new Claim(ClaimNames.Mfa, "true"));
        }

        return Sign(claims, _options.Audience, _options.ExpiresMinutes);
    }

    public string CreateMfaToken(ApplicationUser user)
    {
        List<Claim> claims = new List<Claim>
        {
            new Claim(ClaimNames.Sub, user.Id.ToString()),
            new Claim(MfaTokenConstants.PurposeClaim, MfaTokenConstants.PurposeValue)
        };

        return Sign(claims, MfaTokenConstants.Audience, 5);
    }

    private string Sign(IEnumerable<Claim> claims, string audience, int lifetimeMinutes)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(lifetimeMinutes),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
