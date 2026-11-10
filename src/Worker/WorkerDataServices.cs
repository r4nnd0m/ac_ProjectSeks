using Sims.Shared.Audit;
using Sims.Shared.Escalation;
using Sims.Shared.Extensions;
using Sims.Shared.Tickets;
using Worker.Escalation;
using Worker.Scanning;

namespace Worker;

public static class WorkerDataServices
{
    public static IServiceCollection AddWorkerRules(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddVerdictRules(configuration);
        services.AddEscalation(configuration);
        services.AddSingleton<IScanVerdictCalculator, ScanVerdictAdapter>();
        services.AddSingleton<IEscalationPolicy, EscalationPolicyAdapter>();
        return services;
    }

    public static IServiceCollection AddWorkerDataServices(this IServiceCollection services, string? connectionString)
    {
        bool hasDatabase = !string.IsNullOrWhiteSpace(connectionString);
        bool hasTicketPolicy = services.Any(s => s.ServiceType == typeof(EscalationPolicy));
        if (hasDatabase)
        {
            services.AddSimsDatabase(connectionString!);
            services.AddScoped<AuditService>();
            services.AddScoped<IScanStore, SqlScanStore>();
            services.AddScoped<IEscalationStore, SqlEscalationStore>();
            services.AddScoped<IEscalationAudit, SqlEscalationAudit>();
            if (hasTicketPolicy)
            {
                services.AddScoped<TicketCreator>();
                services.AddScoped<IScanTicketPlanner, SqlTicketPlanner>();
            }
        }

        if (hasDatabase && hasTicketPolicy && services.Any(s => s.ServiceType == typeof(IScanVerdictCalculator)))
        {
            services.AddScoped<IScanProcessor, ScanProcessor>();
            services.AddScoped<IScanRecovery, ScanRecovery>();
        }
        else
        {
            services.AddScoped<IScanProcessor, UnconfiguredScanProcessor>();
            services.AddScoped<IScanRecovery, UnconfiguredScanRecovery>();
        }

        if (hasDatabase && services.Any(s => s.ServiceType == typeof(IEscalationPolicy)))
            services.AddScoped<IEscalationProcessor, EscalationProcessor>();
        else
            services.AddScoped<IEscalationProcessor, UnconfiguredEscalationProcessor>();

        return services;
    }
}
