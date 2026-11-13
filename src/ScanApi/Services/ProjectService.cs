using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ScanApi.Extensions;
using ScanApi.Models;
using Sims.Shared.Data;
using Sims.Shared.Entities;

namespace ScanApi.Services;

public interface IProjectService
{
    Task<List<ProjectResponse>> ListAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<ProjectResponse?> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);
    Task<ProjectResponse> CreateAsync(Guid teamId, string name, Guid ownerId, CancellationToken ct = default);
}

public class ProjectService(SimsDbContext db) : IProjectService
{
    public async Task<List<ProjectResponse>> ListAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        return await Visible(user)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectResponse(p.Id, p.Name, p.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ProjectResponse?> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        return await Visible(user)
            .Where(p => p.Id == id)
            .Select(p => new ProjectResponse(p.Id, p.Name, p.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ProjectResponse> CreateAsync(Guid teamId, string name, Guid ownerId, CancellationToken ct = default)
    {
        var project = new Project
        {
            TeamId = teamId,
            OwnerId = ownerId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return new ProjectResponse(project.Id, project.Name, project.CreatedAt);
    }

    private IQueryable<Project> Visible(ClaimsPrincipal user)
    {
        var projects = db.Projects.AsNoTracking();
        if (user.IsAdmin())
            return projects;

        var teamId = user.GetTeamId();
        return projects.Where(p => p.TeamId == teamId);
    }
}
