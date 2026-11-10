using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sims.Contracts;
using Sims.Shared.Data;
using Sims.Shared.Entities;
using ScanApi.Models;

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
        var teamId = Guid.Parse(user.FindFirstValue(ClaimNames.TeamId)!);
        var isAdmin = user.IsInRole(Roles.Admin);

        var query = db.Projects.AsNoTracking().AsQueryable();
        if (!isAdmin)
            query = query.Where(p => p.TeamId == teamId);

        var items = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return items.Select(p => new ProjectResponse(p.Id, p.Name, p.CreatedAt)).ToList();
    }

    public async Task<ProjectResponse?> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var teamId = Guid.Parse(user.FindFirstValue(ClaimNames.TeamId)!);
        var isAdmin = user.IsInRole(Roles.Admin);

        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && (isAdmin || p.TeamId == teamId), ct);
        return project is null ? null : new ProjectResponse(project.Id, project.Name, project.CreatedAt);
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
}
