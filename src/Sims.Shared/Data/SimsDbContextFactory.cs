using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sims.Shared.Data;

// Wird nur von dotnet ef verwendet, damit Migrationen ohne JWT- und Redis-Konfiguration der Scan-API laufen.
public sealed class SimsDbContextFactory : IDesignTimeDbContextFactory<SimsDbContext>
{
    public SimsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Sims")
            ?? "Server=127.0.0.1,1433;Database=sims;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<SimsDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "scan"))
            .Options;

        return new SimsDbContext(options);
    }
}
