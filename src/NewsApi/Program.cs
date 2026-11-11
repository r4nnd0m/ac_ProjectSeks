
using System.Net.Http.Headers;
using NewsApi.Extensions;
using NewsApi.Options;
using NewsApi.Services;
using Scalar.AspNetCore;

namespace sims;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();

        // N2: Absicherung nach S1 und Health-Check
        builder.Services.AddSimsSecurity(builder.Configuration);
        builder.Services.AddHealthChecks();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        // N1: Feste Feed-Liste. Startet nur, wenn jede URL absolut und https ist.
        builder.Services.AddOptions<FeedOptions>()
            .Bind(builder.Configuration.GetSection(FeedOptions.SectionName))
            .Validate(o => o.ActiveUrls.Any(), "Feeds:Urls braucht mindestens eine URL.")
            .Validate(o => o.ActiveUrls.All(FeedOptions.IsAllowedUrl), "Feeds:Urls darf nur absolute https-URLs enthalten.")
            .ValidateOnStart();

        builder.Services.AddOptions<NewsOptions>()
            .Bind(builder.Configuration.GetSection(NewsOptions.SectionName))
            .Validate(o => o.RefreshMinutes is > 0 and <= 1440, "News:RefreshMinutes muss zwischen 1 und 1440 liegen.")
            .ValidateOnStart();

        // N1: HttpClient für die Feeds – 10 s Zeitlimit, höchstens 2 MB, eigener User-Agent.
        builder.Services.AddHttpClient<IFeedReader, RssFeedReader>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(10);
            http.MaxResponseContentBufferSize = 2 * 1024 * 1024;
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SIMS-News", "1.0"));
        });

        // N2: Cache und NewsService
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<NewsService>();

        var app = builder.Build();

        app.UseSimsSecurity();

        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference();
            app.MapOpenApi();
        }

        app.MapControllers();
        app.MapHealthChecks("/health");

        app.Run();
    }
}
