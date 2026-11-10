using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sims.Shared.Data;

namespace Sims.Shared.Extensions;

public static class SimsDatabaseExtensions
{
    public static IServiceCollection AddSimsDatabase(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SimsDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "scan")));

        return services;
    }
}
