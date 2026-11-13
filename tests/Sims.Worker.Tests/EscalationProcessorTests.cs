using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Sims.Contracts;
using Worker.Escalation;

namespace Sims.Worker.Tests;

public sealed class EscalationProcessorTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AppliesProvidedStepsAndRecordsEachChangeWithOneSavePerCycle()
    {
        var clock = new FakeTimeProvider(Start);
        var ticket = new TestTicket { DueAt = Start.AddSeconds(-1) };
        var store = new Store(ticket);
        var steps = new Queue<EscalationStep>([
            new(2, Roles.Lead, Start.AddSeconds(36)), new(3, Roles.Admin, null)
        ]);
        var policy = new Policy((_, _) => steps.Dequeue());
        var processor = Create(store, policy, clock);

        await processor.RunAsync(CancellationToken.None);
        Assert.Equal(2, ticket.EscalationLevel);
        Assert.Equal(Roles.Lead, ticket.AssignedRole);
        Assert.Equal("Escalated", ticket.Status);
        Assert.Equal(Start.AddSeconds(36), ticket.DueAt);
        clock.Advance(TimeSpan.FromSeconds(36));
        await processor.RunAsync(CancellationToken.None); // DueAt == now reicht noch nicht.
        Assert.Single(store.CommittedAudit);
        clock.Advance(TimeSpan.FromSeconds(1));
        await processor.RunAsync(CancellationToken.None);
        Assert.Equal(3, ticket.EscalationLevel);
        Assert.Equal(Roles.Admin, ticket.AssignedRole);
        Assert.Null(ticket.DueAt);
        clock.Advance(TimeSpan.FromDays(1));
        await processor.RunAsync(CancellationToken.None);

        Assert.Equal(2, policy.Calls.Count);
        Assert.Equal(4, store.Saves);
        Assert.Equal(new[] { "Stufe 1 auf 2", "Stufe 2 auf 3" }, store.CommittedAudit.Select(e => e.Details));
        Assert.All(store.CommittedAudit, entry =>
        {
            Assert.Equal(Guid.Empty, entry.UserId);
            Assert.Equal("SIMS Worker", entry.UserName);
            Assert.Equal(ticket.Id, entry.TicketId);
        });
    }

    [Theory]
    [InlineData("Closed", 1, -1)]
    [InlineData("RiskAccepted", 1, -1)]
    [InlineData("Open", 3, -1)]
    [InlineData("Open", 1, null)]
    [InlineData("Open", 1, 0)]
    [InlineData("Open", 1, 1)]
    public async Task IgnoresFinishedFinalAndNotOverdueTickets(string status, int level, int? dueSeconds)
    {
        var ticket = new TestTicket { Status = status, EscalationLevel = level, DueAt = dueSeconds is null ? null : Start.AddSeconds(dueSeconds.Value) };
        var store = new Store(ticket);
        var policy = new Policy((_, _) => throw new InvalidOperationException("Policy must not be called."));
        await Create(store, policy, new FakeTimeProvider(Start)).RunAsync(CancellationToken.None);
        Assert.Empty(policy.Calls);
        Assert.Empty(store.CommittedAudit);
        Assert.Equal(0, ticket.Changes);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task UsesOneClockSnapshotAndSavesAllChangesTogether()
    {
        var clock = new FakeTimeProvider(Start);
        var store = new Store(new TestTicket { DueAt = Start.AddSeconds(-1) }, new TestTicket { DueAt = Start.AddSeconds(-1) });
        using var stop = new CancellationTokenSource();
        var policy = new Policy((_, now) =>
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            return new(2, Roles.Lead, now.AddHours(2));
        });
        await Create(store, policy, clock).RunAsync(stop.Token);
        Assert.Equal(Start, store.QueriedAt);
        Assert.All(policy.Calls, now => Assert.Equal(Start, now));
        Assert.All(store.Tickets, t => Assert.Equal(Start.AddHours(2), t.DueAt));
        Assert.Equal(2, store.CommittedAudit.Count);
        Assert.Equal(1, store.Saves);
        Assert.Equal(stop.Token, store.LoadToken);
        Assert.Equal(stop.Token, store.SaveToken);
    }

    [Fact]
    public async Task NullPolicyDecisionLeavesTheTicketAndAuditUntouched()
    {
        var ticket = new TestTicket { DueAt = Start.AddSeconds(-1) };
        var store = new Store(ticket);
        await Create(store, new Policy((_, _) => null), new FakeTimeProvider(Start)).RunAsync(CancellationToken.None);
        Assert.Equal(0, ticket.Changes);
        Assert.Empty(store.CommittedAudit);
        Assert.Equal(1, store.Saves);
    }

    [Theory]
    [InlineData("load")]
    [InlineData("policy")]
    [InlineData("ticket")]
    [InlineData("audit")]
    [InlineData("save")]
    public async Task AFailurePreventsCommittingPartialResults(string failAt)
    {
        var first = new TestTicket { DueAt = Start.AddSeconds(-1) };
        var second = new TestTicket { DueAt = Start.AddSeconds(-1), FailEscalate = failAt == "ticket" };
        var store = new Store(first, second) { Failure = failAt };
        var policy = new Policy((ticket, _) => failAt == "policy" && ticket.Id == second.Id
            ? throw new InvalidOperationException("Test") : new(2, Roles.Lead, Start.AddHours(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(store, policy, new FakeTimeProvider(Start)).RunAsync(CancellationToken.None));
        Assert.Empty(store.CommittedAudit);
        Assert.Equal(0, store.CommittedTicketChanges);
        Assert.Equal(failAt == "save" ? 1 : 0, store.Saves);
    }

    [Theory]
    [InlineData("before")]
    [InlineData("after-load")]
    [InlineData("after-audit")]
    public async Task CancellationDoesNotSavePreparedChanges(string point)
    {
        using var stop = new CancellationTokenSource();
        var store = new Store(new TestTicket { DueAt = Start.AddSeconds(-1) });
        if (point == "before") stop.Cancel();
        if (point == "after-load") store.AfterLoad = stop.Cancel;
        if (point == "after-audit") store.AfterAudit = stop.Cancel;
        var processor = Create(store, new Policy((_, _) => new(2, Roles.Lead, Start.AddHours(1))), new FakeTimeProvider(Start));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.RunAsync(stop.Token));
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.CommittedAudit);
    }

    private static EscalationProcessor Create(Store store, IEscalationPolicy policy, TimeProvider clock) =>
        new(store, policy, store, clock, NullLogger<EscalationProcessor>.Instance);

    private sealed class TestTicket : IEscalationTicket
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Status { get; set; } = "Open";
        public bool IsFinished => Status is "Closed" or "RiskAccepted";
        public int EscalationLevel { get; set; } = 1;
        public DateTimeOffset? DueAt { get; set; }
        public string? AssignedRole { get; private set; }
        public bool FailEscalate { get; init; }
        public int Changes { get; private set; }
        public void Escalate(int newLevel, string assignedRole, DateTimeOffset? newDueAt)
        {
            if (FailEscalate) throw new InvalidOperationException("Test");
            EscalationLevel = newLevel;
            AssignedRole = assignedRole;
            DueAt = newDueAt;
            Status = "Escalated";
            Changes++;
        }
    }

    private sealed class Policy(Func<IEscalationTicket, DateTimeOffset, EscalationStep?> next) : IEscalationPolicy
    {
        public List<DateTimeOffset> Calls { get; } = [];
        public EscalationStep? NextLevel(IEscalationTicket ticket, DateTimeOffset now) { Calls.Add(now); return next(ticket, now); }
    }

    // Prüft nur die Aufrufreihenfolge, die Transaktion selbst testet SqlWorkerIntegrationTests.
    private sealed class Store(params TestTicket[] tickets) : IEscalationStore, IEscalationAudit
    {
        public TestTicket[] Tickets => tickets;
        private readonly List<EscalationAuditEntry> pendingAudit = [];
        public List<EscalationAuditEntry> CommittedAudit { get; } = [];
        public int CommittedTicketChanges { get; private set; }
        public int Saves { get; private set; }
        public DateTimeOffset QueriedAt { get; private set; }
        public CancellationToken LoadToken { get; private set; }
        public CancellationToken SaveToken { get; private set; }
        public string? Failure { get; init; }
        public Action? AfterLoad { get; set; }
        public Action? AfterAudit { get; set; }
        public Task<IReadOnlyList<IEscalationTicket>> GetDueTicketsAsync(DateTimeOffset now, CancellationToken ct)
        {
            if (Failure == "load") throw new InvalidOperationException("Test");
            QueriedAt = now;
            LoadToken = ct;
            AfterLoad?.Invoke();
            return Task.FromResult<IReadOnlyList<IEscalationTicket>>(tickets);
        }
        public void RecordEscalation(EscalationAuditEntry entry)
        {
            if (Failure == "audit" && pendingAudit.Count == 1) throw new InvalidOperationException("Test");
            pendingAudit.Add(entry);
            AfterAudit?.Invoke();
        }
        public Task SaveChangesAsync(CancellationToken ct)
        {
            Saves++;
            SaveToken = ct;
            if (Failure == "save") throw new InvalidOperationException("Test");
            CommittedAudit.AddRange(pendingAudit);
            pendingAudit.Clear();
            CommittedTicketChanges = tickets.Sum(t => t.Changes);
            return Task.CompletedTask;
        }
    }
}
