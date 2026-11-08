using System.Security.Claims;
using Sims.Contracts;

namespace ScanApi.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimNames.Sub)!);
    public static Guid GetTeamId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimNames.TeamId)!);
    public static string GetUserName(this ClaimsPrincipal user) => user.FindFirstValue(ClaimNames.Name) ?? string.Empty;
    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
}
