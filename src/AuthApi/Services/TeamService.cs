using AuthApi.Data;
using AuthApi.Dtos;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

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
        Team team = new Team { Name = request.Name };
        _db.Teams.Add(team);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return null;
        }

        return new TeamResponse(team.Id, team.Name);
    }
}