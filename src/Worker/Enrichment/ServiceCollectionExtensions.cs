using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Worker.Enrichment;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddKevClient(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddSingleton<KevCatalogCache>();
        var client = services.AddHttpClient<IKevClient, KevClient>(http => http.BaseAddress = new Uri("https://www.cisa.gov/"));
        client.AddStandardResilienceHandler();
        return client;
    }

    public static IHttpClientBuilder AddEpssClient(this IServiceCollection services)
    {
        var client = services.AddHttpClient<IEpssClient, EpssClient>(http => http.BaseAddress = new Uri("https://api.first.org/"));
        client.AddStandardResilienceHandler();
        return client;
    }
}
