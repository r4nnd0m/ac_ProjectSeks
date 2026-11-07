using System.Security.Claims;
using AuthApi.Models;
using AuthApi.Options;
using Microsoft.Extensions.Options;
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


        throw new NotImplementedException();
    }
}