using Sims.Shared.Entities;
using Sims.Shared.Enums;
using Xunit;

public class TicketTests
{
    private static Ticket CreateTicket()
    {
        NewTicket data = new NewTicket(
            Guid.NewGuid(), "Log4j-Lücke", "Webserver betroffen",
            Severity.High, Guid.NewGuid(), "Anna");

        return new Ticket(data, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void New_ticket_is_open_with_level_1()
    {
        Ticket ticket = CreateTicket();

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(1, ticket.EscalationLevel);
    }

    [Fact]
    public void Escalate_raises_level_and_sets_role_and_status()
    {
        Ticket ticket = CreateTicket();
        DateTimeOffset due = DateTimeOffset.UtcNow.AddHours(24);

        ticket.Escalate(2, "Lead", due);

        Assert.Equal(2, ticket.EscalationLevel);
        Assert.Equal("Lead", ticket.AssignedRole);
        Assert.Equal(due, ticket.DueAt);
        Assert.Equal(TicketStatus.Escalated, ticket.Status);
    }

    [Fact]
    public void Escalate_to_lower_level_throws()
    {
        Ticket ticket = CreateTicket();
        ticket.Escalate(3, "Admin", null);

        Assert.Throws<ArgumentException>(() => ticket.Escalate(2, "Lead", null));
    }

    [Fact]
    public void Escalate_on_closed_ticket_throws()
    {
        Ticket ticket = CreateTicket();
        ticket.Close(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => ticket.Escalate(2, "Lead", null));
    }

    [Fact]
    public void Close_sets_status_and_closed_at()
    {
        Ticket ticket = CreateTicket();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        ticket.Close(now);

        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.Equal(now, ticket.ClosedAt);
    }

    [Fact]
    public void Second_close_keeps_first_closed_at()
    {
        Ticket ticket = CreateTicket();
        DateTimeOffset first = DateTimeOffset.UtcNow;
        DateTimeOffset second = first.AddHours(1);

        ticket.Close(first);
        ticket.Close(second);

        Assert.Equal(first, ticket.ClosedAt);
    }

    [Fact]
    public void Assign_sets_assignee_and_in_progress()
    {
        Ticket ticket = CreateTicket();
        Guid userId = Guid.NewGuid();

        ticket.Assign(userId, "Ben");

        Assert.Equal(userId, ticket.AssigneeId);
        Assert.Equal("Ben", ticket.AssigneeName);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
    }

    [Fact]
    public void Assign_on_closed_ticket_throws()
    {
        Ticket ticket = CreateTicket();
        ticket.Close(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => ticket.Assign(Guid.NewGuid(), "Ben"));
    }

    [Fact]
    public void Update_changes_title_description_and_severity()
    {
        Ticket ticket = CreateTicket();

        ticket.Update("Neuer Titel", "Neue Beschreibung", Severity.Critical);

        Assert.Equal("Neuer Titel", ticket.Title);
        Assert.Equal("Neue Beschreibung", ticket.Description);
        Assert.Equal(Severity.Critical, ticket.Severity);
    }

    [Fact]
    public void Update_on_closed_ticket_throws()
    {
        Ticket ticket = CreateTicket();
        ticket.Close(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => ticket.Update("Titel", "Text", Severity.Low));
    }
}