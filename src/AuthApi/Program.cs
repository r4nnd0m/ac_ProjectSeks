using AuthApi.Data;
using AuthApi.Models;
using AuthApi.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

namespace sims;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<AuthDbContext>(o =>
            o.UseSqlServer(builder.Configuration.GetConnectionString("Auth"),
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "auth")));

        builder.Services
            .AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequiredLength = 12;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.AddDataProtection();

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        builder.Services.AddHealthChecks();

        builder.Services.AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection("Jwt"))
            .Validate(o => o.Key.Length >= 32, "Der Key ist zu kurz!")
            .ValidateOnStart();

        var app = builder.Build();

        if (app.Configuration["Migrations:Run"] == "true")
        {
            using IServiceScope scope = app.Services.CreateScope();
            AuthDbContext db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            await MigrateWithRetryAsync(db, app.Logger);

            UserManager<ApplicationUser> userManager =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager<IdentityRole<Guid>> roleManager =
                scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            await DbSeeder.SeedAsync(db, userManager, roleManager, app.Configuration);
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference();
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();


        app.MapControllers();

        app.MapHealthChecks("/health");

        app.Run();
    }
    private static async Task MigrateWithRetryAsync(AuthDbContext db, ILogger log)
        {
            for (int i = 1; i <= 10; i++)
            {
                try
                {
                    await db.Database.MigrateAsync();
                    return;
                }
                catch (Exception ex)
                {
                    log.LogWarning("Versuch {Versuch} von 10 bei der Migration fehlgeschlagen: {Fehler}", i, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            throw new Exception("Die Datenbank ist nach 10 Versuchen nicht erreichbar.");
        }
}