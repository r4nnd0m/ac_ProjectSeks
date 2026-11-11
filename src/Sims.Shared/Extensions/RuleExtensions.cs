using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sims.Shared.Escalation;
using Sims.Shared.Rules;

namespace Sims.Shared.Extensions;

public static class RuleExtensions
{
    public static IServiceCollection AddVerdictRules(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<VerdictOptions>().Bind(configuration.GetSection("Verdict"))
            .Validate(o => o.EpssNoGoThreshold is >= 0 and <= 1, "EPSS muss zwischen 0 und 1 liegen.")
            .ValidateOnStart();
        services.AddSingleton<IVerdictRule, KevRule>();
        services.AddSingleton<IVerdictRule, CvssRule>();
        services.AddSingleton<IVerdictRule, EpssRule>();
        services.AddSingleton<VerdictCalculator>();
        return services;
    }

    public static IServiceCollection AddEscalation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EscalationOptions>().Bind(configuration.GetSection("Escalation"))
            .Validate(o => ValidHours(o.CriticalHours) && ValidHours(o.HighHours) && ValidHours(o.MediumHours),
                "Eskalationsfristen müssen positive, endliche Stundenwerte sein.")
            .ValidateOnStart();
        services.AddSingleton<EscalationPolicy>();
        return services;
    }

    private static bool ValidHours(double hours) => double.IsFinite(hours) && hours > 0 && hours < TimeSpan.MaxValue.TotalHours;
}
