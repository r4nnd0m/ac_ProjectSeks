using Sims.Contracts;
using Sims.Shared.Entities;
using Sims.Shared.Enums;
using Sims.Shared.Escalation;
using Sims.Shared.Tickets;
using Xunit;

public class TicketCreatorTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static TicketCreator CreateCreator()
    {
        return new TicketCreator(new EscalationPolicy());
    }

    private static Project CreateProject()
    {
        return new Project { Name = "Shop", TeamId = Guid.NewGuid(), OwnerId = Guid.NewGuid() };
    }

    private static Finding CreateFinding(string cve, Severity severity, string component = "Newtonsoft.Json", bool isKev = false)
    {
        return new Finding
        {
            VulnId = cve,
            Cve = cve,
            Severity = severity,
            IsKev = isKev,
            Component = new Component { Name = component, Version = "12.0.1" }
        };
    }

    private static Ticket CreateExistingTicket(Project project, string cve, string system)
    {
        NewTicket data = new NewTicket(
            project.TeamId, "Alt", "Alt", Severity.High, Guid.Empty, "SIMS Scanner",
            ProjectId: project.Id, Cve: cve, System: system);

        return new Ticket(data, Now);
    }

    [Fact]
    public void Low_without_kev_creates_no_ticket()
    {
        Project project = CreateProject();
        List<Finding> findings = [CreateFinding("CVE-2026-0001", Severity.Low)];

        IReadOnlyList<Ticket> result = CreateCreator().CreateFor(project, findings, [], Now);

        Assert.Empty(result);
    }

    [Fact]
    public void Medium_creates_ticket_on_level_1()
    {
        Project project = CreateProject();
        List<Finding> findings = [CreateFinding("CVE-2026-0002", Severity.Medium)];

        IReadOnlyList<Ticket> result = CreateCreator().CreateFor(project, findings, [], Now);

        Ticket ticket = Assert.Single(result);
        Assert.Equal(1, ticket.EscalationLevel);
        Assert.Equal(project.TeamId, ticket.TeamId);
    }

    [Fact]
    public void Low_with_kev_creates_ticket_on_level_3_for_admin()
    {
        Project project = CreateProject();
        List<Finding> findings = [CreateFinding("CVE-2026-0003", Severity.Low, isKev: true)];

        IReadOnlyList<Ticket> result = CreateCreator().CreateFor(project, findings, [], Now);

        Ticket ticket = Assert.Single(result);
        Assert.Equal(3, ticket.EscalationLevel);
        Assert.Equal(Roles.Admin, ticket.AssignedRole);
    }

    [Fact]
    public void Existing_ticket_prevents_duplicate()
    {
        Project project = CreateProject();
        List<Finding> findings = [CreateFinding("CVE-2026-0004", Severity.High)];
        List<Ticket> existing = [CreateExistingTicket(project, "CVE-2026-0004", "Newtonsoft.Json")];

        IReadOnlyList<Ticket> result = CreateCreator().CreateFor(project, findings, existing, Now);

        Assert.Empty(result);
    }

    [Fact]
    public void Same_cve_in_different_components_creates_two_tickets()
    {
        Project project = CreateProject();
        List<Finding> findings =
        [
            CreateFinding("CVE-2026-0005", Severity.High, "Lib.A"),
            CreateFinding("CVE-2026-0005", Severity.High, "Lib.B")
        ];

        IReadOnlyList<Ticket> result = CreateCreator().CreateFor(project, findings, [], Now);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Same_finding_twice_creates_one_ticket()
    {
        Project project = CreateProject();
        Finding finding = CreateFinding("CVE-2026-0006", Severity.High);
        List<Finding> findings = [finding, finding];

        IReadOnlyList<Ticket> result = CreateCreator().CreateFor(project, findings, [], Now);

        Assert.Single(result);
    }

    [Fact]
    public void Closed_existing_ticket_is_no_duplicate()
    {
        Project project = CreateProject();
        List<Finding> findings = [CreateFinding("CVE-2026-0007", Severity.High)];
        Ticket closed = CreateExistingTicket(project, "CVE-2026-0007", "Newtonsoft.Json");
        closed.Close(Now);

        IReadOnlyList<Ticket> result = CreateCreator().CreateFor(project, findings, [closed], Now);

        Assert.Single(result);
    }
    [Fact]
    public void Risk_accepted_ticket_prevents_duplicate()
    {
        Project project = CreateProject();
        List<Finding> findings = [CreateFinding("CVE-2026-0008", Severity.High)];
        Ticket accepted = CreateExistingTicket(project, "CVE-2026-0008", "Newtonsoft.Json");
        accepted.AcceptRisk(Now);

        IReadOnlyList<Ticket> result = CreateCreator().CreateFor(project, findings, [accepted], Now);

        Assert.Empty(result);
    }
    [Fact]
    public void Long_component_name_keeps_title_within_limit_and_cve_readable()
    {
        Project project = CreateProject();
        string longName = new string('a', 200);
        List<Finding> findings = [CreateFinding("CVE-2026-0009", Severity.High, longName)];

        IReadOnlyList<Ticket> result = CreateCreator().CreateFor(project, findings, [], Now);

        Ticket ticket = Assert.Single(result);
        Assert.Equal(Ticket.TitleMaxLength, ticket.Title.Length);
        Assert.StartsWith("CVE-2026-0009 in ", ticket.Title);
    }

    [Fact]
    public void Short_title_stays_unchanged()
    {
        Project project = CreateProject();
        List<Finding> findings = [CreateFinding("CVE-2026-0010", Severity.High)];

        IReadOnlyList<Ticket> result = CreateCreator().CreateFor(project, findings, [], Now);

        Ticket ticket = Assert.Single(result);
        Assert.Equal("CVE-2026-0010 in Newtonsoft.Json 12.0.1", ticket.Title);
    }
}