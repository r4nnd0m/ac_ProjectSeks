using System.Security.Claims;

namespace Sims.Contracts;

public static class AccessTokenPolicy
{
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public static string RevocationKey(string jti) => $"auth:blacklist:{jti}";

    public static bool HasUserAndTeam(ClaimsPrincipal? principal) =>
        Guid.TryParse(principal?.FindFirst(ClaimNames.Sub)?.Value, out _) &&
        Guid.TryParse(principal?.FindFirst(ClaimNames.TeamId)?.Value, out _);
}
