using AuthApi.Data;
using AuthApi.Dtos;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class TeamService
{
    private readonly AuthDbContext _db;

    public TeamService(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<List<TeamResponse>> ListAsync()
    {
        return await _db.Teams
            .OrderBy(t => t.Name)
            .Select(t => new TeamResponse(t.Id, t.Name))
            .ToListAsync();
    }

    public async Task<TeamResponse?> CreateAsync(CreateTeamRequest request)
    {
        if (await _db.Teams.AnyAsync(t => t.Name == request.Name))
        {
            return null;
        }

        Team team = new Team { Name = request.Name };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync();

        return new TeamResponse(team.Id, team.Name);
    }
}