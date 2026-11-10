using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using ScanApi.Controllers;
using ScanApi.Models;
using ScanApi.Services;
using Sims.Contracts;
using Sims.Shared.Audit;
using Sims.Shared.Data;
using Sims.Shared.Entities;
using Sims.Shared.Enums;
using Worker.Enrichment;
using Worker.Escalation;
using Worker.Osv;
using Worker.Scanning;

namespace Sims.Worker.Tests;

public sealed class SqlWorkerIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [SqlServerFact]
    public async Task TwoUploadsAndAnOutage_StoreFindingsOncePerScan_AndDeduplicateTickets()
    {
        await WithDatabase(async services =>
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SimsDbContext>();
            var project = new Project { TeamId = Guid.NewGuid(), Name = "SQL-Test", CreatedAt = Now };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            var api = new ScansController(db, new CycloneDxParser());
            async Task<Guid> Upload(string version, string purl)
            {
                var bom = new CycloneDxBom("CycloneDX", "1.5", [new("Example", "1", purl)]);
                var accepted = Assert.IsType<AcceptedResult>(await api.Upload(project.Id, version, bom));
                var response = Assert.IsType<ScanCreatedResponse>(accepted.Value);
                Assert.Equal("Pending", response.Status);
                return response.ScanId;
            }
            var firstId = await Upload("1", "pkg:nuget/example@1");
            var secondId = await Upload("2", "pkg:nuget/example@1");
            var failedId = await Upload("3", "pkg:nuget/outage@1");
            db.ChangeTracker.Clear();
            Assert.True(await scope.ServiceProvider.GetRequiredService<IScanStore>().TryStartAsync(firstId, default));

            await scope.ServiceProvider.GetRequiredService<IScanRecovery>().RecoverAsync(default);
            var processor = scope.ServiceProvider.GetRequiredService<global::Worker.IScanProcessor>();
            await processor.ProcessPendingAsync(default);
            await processor.ProcessPendingAsync(default);
            async Task<ScanDetailResponse> ReadResult(Guid id)
            {
                var action = await api.GetById(id);
                return Assert.IsType<ScanDetailResponse>(Assert.IsType<OkObjectResult>(action.Result).Value);
            }
            var first = await ReadResult(firstId);
            var second = await ReadResult(secondId);
            foreach (var scan in new[] { first, second })
            {
                Assert.Equal("Scanned", scan.Status);
                Assert.Equal("GoWithConditions", scan.Verdict);
                Assert.NotNull(scan.FinishedAt);
                var finding = Assert.Single(scan.Findings);
                Assert.Equal("Medium", finding.Severity);
                Assert.Equal("Example", finding.Component);
                Assert.Equal(0.2, finding.Epss);
            }
            var failed = await ReadResult(failedId);
            Assert.Equal("Failed", failed.Status);
            Assert.Null(failed.Verdict);
            Assert.NotNull(failed.FinishedAt);
            Assert.Empty(failed.Findings);
            var ticket = await db.Tickets.AsNoTracking().SingleAsync();
            Assert.Equal(project.TeamId, ticket.TeamId);
            Assert.Equal(project.Id, ticket.ProjectId);
            Assert.Equal(first.Findings[0].Id, ticket.FindingId);
        });
    }

    [SqlServerFact]
    public async Task ClaimAndCompletionAreConditional_AndFailedInsertRollsBackTheWholeResult()
    {
        await WithDatabase(async services =>
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SimsDbContext>();
            var scan = NewScan(new Project { Name = "Rollback", CreatedAt = Now }, 1);
            db.Scans.Add(scan);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var store = scope.ServiceProvider.GetRequiredService<IScanStore>();
            var pending = Assert.Single(await store.GetPendingAsync(1, default));
            Assert.True(await store.TryStartAsync(scan.Id, default));
            Assert.False(await store.TryStartAsync(scan.Id, default));
            var component = Assert.Single(await store.GetComponentsAsync(scan.Id, default));
            var finding = new EnrichedFinding(Source.Finding(component), false, 0.2);
            var planner = scope.ServiceProvider.GetRequiredService<IScanTicketPlanner>();
            var tickets = await planner.PrepareAsync(pending, [finding], Now, default);
            Assert.Equal(1, tickets.Count);
            // Die Fremdschlüsselverletzung erzwingt einen echten SQL-Rollback.
            await db.Components.Where(c => c.Id == component.Id).ExecuteDeleteAsync();
            var completion = new ScanCompletion([finding], ScanVerdict.GoWithConditions, tickets, Now);
            var rejected = await Assert.ThrowsAsync<ScanResultRejectedException>(() => store.CompleteAsync(scan.Id, completion, default));
            Assert.IsType<DbUpdateException>(rejected.InnerException);
            Assert.Equal(ScanStatus.Scanning, (await db.Scans.SingleAsync()).Status);
            Assert.Empty(await db.Findings.ToListAsync());
            Assert.Empty(await db.Tickets.ToListAsync());
            await store.FailAsync(scan.Id, "Test", Now, default);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => store.CompleteAsync(scan.Id, completion, default));
            Assert.Equal(0, await store.ResetInterruptedAsync(default));
        });
    }

    [SqlServerFact]
    public async Task RejectedTicketTitle_FailsOnlyItsScan_AndLeavesNoPartialResult()
    {
        await WithDatabase(async services =>
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SimsDbContext>();
            var project = new Project { Name = "Feldgrenze", CreatedAt = Now };
            var invalid = NewScan(project, 1);
            // Ein gültiger B1-Komponentenname kann mit CVE und Version länger als C1.Title werden.
            invalid.Components[0].Name = new string('x', 200);
            var valid = NewScan(project, 2);
            db.Scans.AddRange(invalid, valid);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await scope.ServiceProvider.GetRequiredService<global::Worker.IScanProcessor>().ProcessPendingAsync(default);
            var failed = await db.Scans.AsNoTracking().Include(s => s.Findings).SingleAsync(s => s.Id == invalid.Id);
            Assert.Equal(ScanStatus.Failed, failed.Status);
            Assert.Null(failed.Verdict);
            Assert.Empty(failed.Findings);
            Assert.Equal(ScanStatus.Scanned, (await db.Scans.SingleAsync(s => s.Id == valid.Id)).Status);
            var finding = await db.Findings.SingleAsync();
            Assert.Equal(valid.Id, finding.ScanId);
            Assert.Equal(finding.Id, (await db.Tickets.SingleAsync()).FindingId);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithoutTicketPolicy_KeepsBothJobsIdle_EvenIfVerdictsAreAvailable(bool hasVerdict)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        if (hasVerdict) services.AddScoped<IScanVerdictCalculator, TestVerdict>();
        global::Worker.WorkerDataServices.AddWorkerDataServices(services, "Server=localhost;Database=unused");
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        Assert.NotNull(sp.GetRequiredService<IScanStore>());
        Assert.Null(sp.GetService<IScanTicketPlanner>());
        Assert.NotNull(sp.GetRequiredService<IEscalationStore>());
        Assert.NotNull(sp.GetRequiredService<IEscalationAudit>());
        Assert.IsNotType<global::Worker.ScanProcessor>(sp.GetRequiredService<global::Worker.IScanProcessor>());
        Assert.IsNotType<ScanRecovery>(sp.GetRequiredService<IScanRecovery>());
        Assert.IsNotType<EscalationProcessor>(sp.GetRequiredService<IEscalationProcessor>());
    }

    [SqlServerFact]
    public async Task DueTicketAndAuditAreSavedTogether_ClosedTicketIsIgnored()
    {
        await WithDatabase(async services =>
        {
            var data = new NewTicket(Guid.NewGuid(), "Überfällig", "Test", Severity.High,
                Guid.NewGuid(), "Test", DueAt: Now.AddMinutes(-1));
            var due = new Ticket(data, Now.AddHours(-25));
            var closed = new Ticket(data, Now.AddHours(-25));
            closed.Close(Now);
            await using (var seed = services.CreateAsyncScope())
            {
                var db = seed.ServiceProvider.GetRequiredService<SimsDbContext>();
                db.Tickets.AddRange(due, closed);
                await db.SaveChangesAsync();
            }
            // Jeder Durchlauf benutzt wie der HostedService seinen eigenen Arbeitsbereich.
            for (int i = 0; i < 3; i++)
            {
                await using var scope = services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IEscalationProcessor>().RunAsync(default);
            }
            await using var verify = services.CreateAsyncScope();
            var check = verify.ServiceProvider.GetRequiredService<SimsDbContext>();
            var updated = await check.Tickets.SingleAsync(t => t.Id == due.Id);
            Assert.Equal(TicketStatus.Escalated, updated.Status);
            Assert.Equal(3, updated.EscalationLevel);
            Assert.Equal(Roles.Admin, updated.AssignedRole);
            Assert.Null(updated.DueAt);
            Assert.Equal(1, (await check.Tickets.SingleAsync(t => t.Id == closed.Id)).EscalationLevel);
            var audit = await check.AuditEntries.OrderBy(a => a.Details).ToListAsync();
            Assert.Equal(new[] { "Stufe 1 auf 2", "Stufe 2 auf 3" }, audit.Select(a => a.Details));
            Assert.All(audit, a =>
            {
                Assert.Equal(due.Id, a.TicketId);
                Assert.Equal(AuditActions.TicketEscalated, a.Action);
                Assert.Equal(Guid.Empty, a.UserId);
            });
        });
    }

    [SqlServerFact]
    public async Task RejectedAudit_RollsBackTheEscalation()
    {
        await WithDatabase(async services =>
        {
            await using var setup = services.CreateAsyncScope();
            var db = setup.ServiceProvider.GetRequiredService<SimsDbContext>();
            var data = new NewTicket(Guid.NewGuid(), "Prüffall", "", Severity.High,
                Guid.NewGuid(), "Test", DueAt: Now.AddMinutes(-1));
            db.Tickets.Add(new Ticket(data, Now.AddDays(-1)));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE scan.AuditEntries ADD CHECK (Action <> 'TicketEscalated')");
            await using (var cycle = services.CreateAsyncScope())
            {
                var processor = cycle.ServiceProvider.GetRequiredService<IEscalationProcessor>();
                await Assert.ThrowsAsync<DbUpdateException>(() => processor.RunAsync(default));
            }
            var original = await db.Tickets.AsNoTracking().SingleAsync();
            Assert.Equal(TicketStatus.Open, original.Status);
            Assert.Equal(1, original.EscalationLevel);
            Assert.Empty(await db.AuditEntries.ToListAsync());
        });
    }

    private static Scan NewScan(Project project, int minute, string purl = "pkg:nuget/example@1") =>
        new() { Project = project, Version = minute.ToString(), CreatedAt = Now.AddMinutes(minute),
            Components = [new Component { Name = "Example", Version = "1", Purl = purl }] };

    private static async Task WithDatabase(Func<IServiceProvider, Task> run)
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SIMS_WORKER_TEST_SQL"))
        {
            InitialCatalog = $"sims_worker_test_{Guid.NewGuid():N}"
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Now));
        services.AddScoped<IOsvClient, Source>();
        services.AddScoped<IFindingEnricher, Enricher>();
        services.AddScoped<IScanVerdictCalculator, TestVerdict>();
        // Bewusst nur im SQL-Test: aktuelle vorläufige StartFor-Version aus dem C3-Branch.
        services.AddScoped<Sims.Shared.Escalation.EscalationPolicy>();
        services.AddScoped<IEscalationPolicy, TestSteps>();
        global::Worker.WorkerDataServices.AddWorkerDataServices(services, connection.ConnectionString);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var setup = provider.CreateAsyncScope();
        var db = setup.ServiceProvider.GetRequiredService<SimsDbContext>();
        var migrations = db.Database.GetMigrations().ToArray();
        Assert.NotEmpty(migrations);

        // Erst wenn SQL die neue Datenbank erfolgreich angelegt hat, gehört sie diesem Test.
        var creator = db.GetService<IRelationalDatabaseCreator>();
        await creator.CreateAsync();
        try
        {
            await db.Database.MigrateAsync();
            Assert.Equal(migrations, await db.Database.GetAppliedMigrationsAsync());
            await run(provider);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Source : IOsvClient
    {
        public static OsvFinding Finding(OsvComponent c) => new(c.Id, "OSV-DEMO", "CVE-2030-0001", OsvSeverity.Medium, "Testfund");
        public Task<IReadOnlyList<OsvFinding>> FindVulnerabilitiesAsync(IReadOnlyList<OsvComponent> components, CancellationToken ct)
        {
            if (components.Any(c => c.Purl.Contains("outage"))) throw new OsvUnavailableException("Testausfall");
            return Task.FromResult<IReadOnlyList<OsvFinding>>(components.Select(Finding).ToList());
        }
    }

    private sealed class Enricher : IFindingEnricher
    {
        public Task<IReadOnlyList<EnrichedFinding>> EnrichAsync(IReadOnlyList<OsvFinding> findings, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EnrichedFinding>>(findings.Select(f => new EnrichedFinding(f, false, 0.2)).ToList());
    }

    private sealed class TestVerdict : IScanVerdictCalculator
    {
        public ScanVerdict Calculate(IReadOnlyList<EnrichedFinding> findings) => ScanVerdict.GoWithConditions;
    }

    private sealed class TestSteps : IEscalationPolicy
    {
        public EscalationStep? NextLevel(IEscalationTicket ticket, DateTimeOffset now) =>
            ticket.EscalationLevel == 1 ? new(2, Roles.Lead, now.AddSeconds(-1)) : new(3, Roles.Admin, null);
    }
}

internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SIMS_WORKER_TEST_SQL")))
            Skip = "SIMS_WORKER_TEST_SQL mit Zugang zum SQL-Testserver setzen.";
    }
}
