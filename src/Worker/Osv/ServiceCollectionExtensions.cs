namespace Worker.Osv;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddOsvClient(this IServiceCollection services)
    {
        var client = services.AddHttpClient<IOsvClient, OsvClient>(http =>
            http.BaseAddress = new Uri("https://api.osv.dev/"));

        // querybatch ist trotz POST eine reine Leseabfrage und darf wiederholt werden.
        // Standard: Wiederholungen, Zeitlimits und Pause bei gehäuften Ausfällen.
        client.AddStandardResilienceHandler();
        return client;
    }
}
