using System.Threading.RateLimiting;

namespace ScanApi.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddSimsSecurity(this IServiceCollection services, IConfiguration config)
    {
        services.AddProblemDetails();

        string[] origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0)
            origins = ["http://localhost:8080"];

        services.AddCors(options =>
        {
            options.AddPolicy("web", policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
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

        return app;
    }
}
