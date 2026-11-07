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
        string[] roleNames = { Roles.Developer, Roles.Lead, Roles.Admin };
        foreach (string role in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var team = await db.Teams.FirstOrDefaultAsync(t => t.Name == "Default");
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
                throw new Exception("Admin konnte nicht angelegt werden!");
            }

            await userManager.AddToRoleAsync(admin, Roles.Admin);
        }
    }
}
