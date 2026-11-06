var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<Worker.WorkerOptions>()
    .Bind(builder.Configuration.GetSection(Worker.WorkerOptions.SectionName))
    .Validate(
        options => options.PollIntervalSeconds is >= 1 and <= 86400,
        "Worker:PollIntervalSeconds muss zwischen 1 und 86400 Sekunden liegen.")
    .ValidateOnStart();

builder.Services.AddHostedService<Worker.Worker>();

using var host = builder.Build();
await host.RunAsync();
