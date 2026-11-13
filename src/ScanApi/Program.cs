using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using ScanApi.Extensions;
using ScanApi.Services;
using Sims.Contracts;
using Sims.Shared.Audit;
using Sims.Shared.Data;
using Sims.Shared.Extensions;
using StackExchange.Redis;

namespace ScanApi;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Services.AddOpenApi();
        builder.Services.AddSimsSecurity(builder.Configuration);

        var redisConnection = builder.Configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnection);
            redisOptions.AbortOnConnectFail = false;
            builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
        }

        var jwtKey = builder.Configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
            throw new InvalidOperationException("Jwt:Key fehlt oder ist kürzer als 32 Zeichen.");
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "sims-auth";
        var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "sims-api";

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = AccessTokenPolicy.ClockSkew,
                    RoleClaimType = ClaimNames.Role,
                    NameClaimType = ClaimNames.Name
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var jti = context.Principal?.FindFirst(ClaimNames.Jti)?.Value;
                        if (string.IsNullOrWhiteSpace(jti) || !AccessTokenPolicy.HasUserAndTeam(context.Principal))
                        {
                            context.Fail("Unvollständiges Zugriffstoken.");
                            return;
                        }

                        try
                        {
                            var redis = context.HttpContext.RequestServices.GetService<IConnectionMultiplexer>();
                            if (redis is null)
                            {
                                context.Fail("Redis-Blacklist nicht verfügbar.");
                                return;
                            }

                            var value = await redis.GetDatabase().KeyExistsAsync(AccessTokenPolicy.RevocationKey(jti));
                            if (value)
                            {
                                context.Fail("Token wurde widerrufen.");
                            }
                        }
                        catch
                        {
                            context.Fail("Redis-Blacklist ist nicht verfügbar.");
                        }
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy(Policies.MfaRequired, policy => policy.RequireClaim(ClaimNames.Mfa, "true"));
        });

        var connectionString = builder.Configuration.GetConnectionString("Sims");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Sims fehlt.");
        builder.Services.AddSimsDatabase(connectionString);

        builder.Services.AddScoped<IProjectService, ProjectService>();
        builder.Services.AddScoped<ISbomParser, CycloneDxParser>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddEscalation(builder.Configuration);
        builder.Services.AddScoped<ITicketService, TicketService>();
        builder.Services.AddScoped<AuditService>();
        builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();

        var app = builder.Build();

        if (app.Configuration.GetValue<bool>("Migrations:Run"))
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SimsDbContext>();
            await db.Database.MigrateAsync();
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference();
            app.MapOpenApi();
        }

        app.UseSimsSecurity();
        app.UseCors("web");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.RunAsync();
    }
}
