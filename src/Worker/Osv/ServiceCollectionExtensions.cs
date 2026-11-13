namespace Worker.Osv;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddOsvClient(this IServiceCollection services)
    {
        var client = services.AddHttpClient<IOsvClient, OsvClient>(http =>
            http.BaseAddress = new Uri("https://api.osv.dev/"));

        // querybatch ist zwar POST, aber nur lesend und kann daher wiederholt werden.
        client.AddStandardResilienceHandler();
        return client;
    }
}
