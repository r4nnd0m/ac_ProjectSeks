using System.Text;
using System.Threading.RateLimiting;
using AuthApi.Data;
using AuthApi.Models;
using AuthApi.Options;
using AuthApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Sims.Contracts;
using StackExchange.Redis;

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

        builder.Services.AddSingleton<ITokenService, JwtTokenService>();

        JwtOptions jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = ClaimNames.Role,
                    NameClaimType = ClaimNames.Name
                };
                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async ctx =>
                    {
                        string? jti = ctx.Principal?.FindFirst(ClaimNames.Jti)?.Value;
                        ITokenBlacklist blacklist =
                            ctx.HttpContext.RequestServices.GetRequiredService<ITokenBlacklist>();
                        try
                        {
                            if (jti == null || await blacklist.IsRevokedAsync(jti))
                            {
                                ctx.Fail("Der Token wurde widerrufen!");
                            }
                        }
                        catch (Exception)
                        {
                            ctx.Fail("Die Sperrliste ist nicht verfügbar!");
                        }
                    }
                };
            });

        builder.Services.AddAuthorization(o =>
        {
            o.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));
            o.AddPolicy(Policies.MfaRequired, p => p.RequireClaim(ClaimNames.Mfa, "true"));
        });

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            ConfigurationOptions opt = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis")!);
            opt.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(opt);
        });

        builder.Services.AddSingleton<ITokenBlacklist, RedisTokenBlacklist>();

        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unbekannt",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1)
                }));
        });

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

        app.UseRateLimiter();

        app.UseAuthentication();

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