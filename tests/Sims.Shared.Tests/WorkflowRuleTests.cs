using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sims.Contracts;
using Sims.Shared.Entities;
using Sims.Shared.Enums;
using Sims.Shared.Escalation;
using Sims.Shared.Extensions;
using Sims.Shared.Rules;

namespace Sims.Shared.Tests;

public sealed class WorkflowRuleTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static ServiceProvider Build(params (string Key, string Value)[] settings)
    {
        var config = new ConfigurationManager();
        foreach (var (key, value) in settings) config[key] = value;
        return new ServiceCollection().AddVerdictRules(config).AddEscalation(config).BuildServiceProvider();
    }

    [Fact]
    public void EmptyScanCanProceed()
    {
        using var services = Build();
        Assert.Equal(Verdict.Go, services.GetRequiredService<VerdictCalculator>().Calculate([]));
    }

    [Theory]
    [InlineData(Severity.Low, false, null, Verdict.Go)]
    [InlineData(Severity.Medium, false, null, Verdict.GoWithConditions)]
    [InlineData(Severity.High, false, 0.49, Verdict.GoWithConditions)]
    [InlineData(Severity.Critical, false, null, Verdict.NoGo)]
    [InlineData(Severity.Low, true, null, Verdict.NoGo)]
    [InlineData(Severity.Low, false, 0.9, Verdict.NoGo)]
    [InlineData(Severity.Low, false, 0.5, Verdict.NoGo)]
    public void EvaluatesEachFinding(Severity severity, bool kev, double? epss, Verdict expected)
    {
        using var services = Build();
        var finding = new Finding { Severity = severity, IsKev = kev, Epss = epss };
        Assert.Equal(expected, services.GetRequiredService<VerdictCalculator>().Calculate([finding]));
    }

    [Fact]
    public void StrictestFindingWinsInEitherOrder()
    {
        using var services = Build();
        var calculator = services.GetRequiredService<VerdictCalculator>();
        var medium = new Finding { Severity = Severity.Medium };
        var exploited = new Finding { Severity = Severity.Low, IsKev = true };
        Assert.Equal(Verdict.NoGo, calculator.Calculate([medium, exploited]));
        Assert.Equal(Verdict.NoGo, calculator.Calculate([exploited, medium]));
    }

    [Fact]
    public void ReadsConfiguredThresholdAndFractionalHours()
    {
        using var services = Build(("Verdict:EpssNoGoThreshold", "0.8"), ("Escalation:HighHours", "0.025"));
        var calculator = services.GetRequiredService<VerdictCalculator>();
        Assert.Equal(Verdict.Go, calculator.Calculate([new Finding { Epss = 0.79 }]));
        Assert.Equal(Verdict.NoGo, calculator.Calculate([new Finding { Epss = 0.8 }]));
        Assert.Equal(Now.AddSeconds(90), services.GetRequiredService<EscalationPolicy>()
            .StartFor(Severity.High, false, Now).DueAt);
    }

    [Theory]
    [InlineData(Severity.Critical, 4)]
    [InlineData(Severity.High, 24)]
    [InlineData(Severity.Medium, 72)]
    [InlineData(Severity.Low, null)]
    public void SetsFirstDeadline(Severity severity, int? hours)
    {
        using var services = Build();
        var result = services.GetRequiredService<EscalationPolicy>().StartFor(severity, false, Now);
        Assert.Equal(new EscalationStart(1, null, hours.HasValue ? Now.AddHours(hours.Value) : null), result);
    }

    [Fact]
    public void KevStartsWithAdminAndNoDeadline()
    {
        using var services = Build();
        Assert.Equal(new EscalationStart(3, Roles.Admin, null),
            services.GetRequiredService<EscalationPolicy>().StartFor(Severity.Low, true, Now));
    }

    private static Ticket TicketDue(DateTimeOffset? due) => new(
        new NewTicket(Guid.NewGuid(), "Test", "", Severity.High, Guid.Empty, "SIMS", DueAt: due), Now);

    [Fact]
    public void LongOverdueTicketAdvancesOncePerDeadline()
    {
        using var services = Build();
        var policy = services.GetRequiredService<EscalationPolicy>();
        var ticket = TicketDue(Now.AddDays(-10));
        var lead = Assert.IsType<EscalationStep>(policy.NextLevel(ticket, Now));
        Assert.Equal(new EscalationStep(2, Roles.Lead, Now.AddHours(24)), lead);
        ticket.Escalate(lead.Level, lead.AssignedRole, lead.DueAt);
        Assert.Null(policy.NextLevel(ticket, Now.AddHours(24)));
        var admin = Assert.IsType<EscalationStep>(policy.NextLevel(ticket, Now.AddHours(24).AddTicks(1)));
        Assert.Equal(new EscalationStep(3, Roles.Admin, null), admin);
        ticket.Escalate(admin.Level, admin.AssignedRole, admin.DueAt);
        Assert.Null(policy.NextLevel(ticket, Now.AddDays(30)));
    }

    [Fact]
    public void IgnoresFinishedOrNotOverdueTickets()
    {
        using var services = Build();
        var policy = services.GetRequiredService<EscalationPolicy>();
        var closed = TicketDue(Now.AddHours(-1));
        closed.Close(Now);
        var accepted = TicketDue(Now.AddHours(-1));
        accepted.AcceptRisk(Now);
        foreach (var ticket in new[] { closed, accepted, TicketDue(null), TicketDue(Now), TicketDue(Now.AddDays(1)) })
            Assert.Null(policy.NextLevel(ticket, Now));
    }

    [Fact]
    public void RejectsInvalidConfiguration()
    {
        using var services = Build(("Escalation:MediumHours", "-1"), ("Verdict:EpssNoGoThreshold", "1.1"));
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<EscalationOptions>>().Value);
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<VerdictOptions>>().Value);
    }
}
