using System.Security.Claims;
using Sims.Shared.Entities;

namespace ScanApi.Extensions;

internal static class ProjectQueryExtensions
{
    internal static IQueryable<Project> ForUser(this IQueryable<Project> projects, ClaimsPrincipal user)
    {
        if (user.IsAdmin())
            return projects;

        var teamId = user.GetTeamId();
        return projects.Where(project => project.TeamId == teamId);
    }
}
