using System.Threading.RateLimiting;

namespace NewsApi.Extensions;

/// <summary>
/// Absicherung nach Vorlage S1 (aus der ScanApi übernommen, Namespace angepasst):
/// ProblemDetails, CORS aus Cors:AllowedOrigins, Rate Limiting, Security-Header, Größenlimit.
/// </summary>
public static class SecurityExtensions
{
    public const string CorsPolicy = "web";

    public static IServiceCollection AddSimsSecurity(this IServiceCollection services, IConfiguration config)
    {
        services.AddProblemDetails();

        string[] origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0)
        {
            origins = ["http://localhost:8080"];
        }

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicy, policy =>
                policy.WithOrigins(origins)
                    .WithMethods("GET")
                    .AllowAnyHeader());
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ctx.Connection.RemoteIpAddress?.ToString() ?? "unbekannt",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });

        return services;
    }

    public static WebApplication UseSimsSecurity(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.Use(async (context, next) =>
        {
            // Größenlimit: Der News-Service nimmt keine Daten entgegen.
            var limit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false })
            {
                limit.MaxRequestBodySize = 64 * 1024;
            }

            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Cache-Control"] = "no-store";
            if (!app.Environment.IsDevelopment())
            {
                context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            }

            await next();
        });
        app.UseCors(CorsPolicy);
        app.UseRateLimiter();

        return app;
    }
}
