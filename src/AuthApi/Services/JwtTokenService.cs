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

        SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        SigningCredentials credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes),
            SigningCredentials = credentials
        };

        JsonWebTokenHandler handler = new JsonWebTokenHandler();
        string token = handler.CreateToken(descriptor);
        return token;
    }
}