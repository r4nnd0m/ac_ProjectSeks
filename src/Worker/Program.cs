using Sims.Logging;
using StackExchange.Redis;
using Worker.Osv;
using Worker.Enrichment;
using Worker.Escalation;
using Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<Worker.WorkerOptions>()
    .Bind(builder.Configuration.GetSection(Worker.WorkerOptions.SectionName))
    .Validate(
        options => options.PollIntervalSeconds is >= 1 and <= 86400,
        "Worker:PollIntervalSeconds muss zwischen 1 und 86400 Sekunden liegen.")
    .ValidateOnStart();

var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(redisConnection))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Redis fehlt. Lokal in den Worker-Benutzergeheimnissen " +
        "oder als Umgebungsvariable ConnectionStrings__Redis hinterlegen.");
}

ConfigurationOptions redisOptions;
try
{
    redisOptions = ConfigurationOptions.Parse(redisConnection);
}
catch (ArgumentException)
{
    throw new InvalidOperationException("ConnectionStrings:Redis hat ein ungültiges Format.");
}

redisOptions.AbortOnConnectFail = false;
redisOptions.ConnectRetry = 0;
redisOptions.ConnectTimeout = 1000;
redisOptions.BacklogPolicy = BacklogPolicy.FailFast;
redisOptions.ClientName = "sims-worker";

using var redis = await ConnectionMultiplexer.ConnectAsync(redisOptions);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Logging.AddRedis(redis, "worker");
builder.Services.AddWorkerDataServices(builder.Configuration.GetConnectionString("Sims"));
builder.Services.AddOsvClient();
builder.Services.AddKevClient();
builder.Services.AddEpssClient();
builder.Services.AddScoped<IFindingEnricher, FindingEnricher>();
builder.Services.AddHostedService<Worker.Worker>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<Worker.HeartbeatJob>();
builder.Services.AddHostedService<EscalationJob>();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Worker.Worker>>();
if (redis.IsConnected)
    logger.LogInformation("Redis-Verbindung hergestellt. Logmeldungen werden zusätzlich an Redis gesendet.");
else
    logger.LogWarning("Redis derzeit nicht erreichbar. Der Worker startet mit Konsolenmeldungen weiter.");

await host.RunAsync();
await redis.CloseAsync(allowCommandsToComplete: true);
