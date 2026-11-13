using AuthApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sims.Contracts;

namespace AuthApi.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AuthDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IConfiguration config)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        foreach (string role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var created = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                if (!created.Succeeded)
                    throw new InvalidOperationException($"Rolle {role} konnte nicht angelegt werden.");
            }
        }

        Team? team = await db.Teams.FirstOrDefaultAsync(t => t.Name == "Default");
        if (team == null)
        {
            team = new Team { Name = "Default" };
            db.Teams.Add(team);
            await db.SaveChangesAsync();
        }

        string? email = config["Admin:Email"];
        string? password = config["Admin:Password"];

        if (!await userManager.Users.AnyAsync() && email != null && password != null)
        {
            ApplicationUser admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                TeamId = team.Id
            };

            IdentityResult result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Admin konnte nicht angelegt werden.");
            }

            var assigned = await userManager.AddToRoleAsync(admin, Roles.Admin);
            if (!assigned.Succeeded)
                throw new InvalidOperationException("Admin-Rolle konnte nicht zugewiesen werden.");
        }

        await transaction.CommitAsync();
    }
}
