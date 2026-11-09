using Xunit;

public class TicketTests

{
    [Fact]
    public void New_ticket_is_open()
    {

        Ticket ticket = new Ticket("Log4j-Lücke", "Webserver betroffen", DateTimeOffset.UtcNow);

        Assert.Equal(TicketStatus.Open, ticket.Status);
    }
        
    [Fact]
    public void Escalate_raises_level_and_sets_status()
    {
        Ticket ticket = new Ticket("Log4j-Lücke", "Webserver betroffen", DateTimeOffset.UtcNow);

        ticket.Escalate(2);

        Assert.Equal(2, ticket.EscalationLevel);
        Assert.Equal(TicketStatus.Escalated, ticket.Status);
    }
    [Fact]
    public void Escalate_on_closed_ticket_throws()
    {
        Ticket ticket = new Ticket("Log4j-Lücke", "Webserver betroffen", DateTimeOffset.UtcNow);
        ticket.Close(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => ticket.Escalate(2));
    }
    [Fact]
    public void Escalate_to_lower_level_throws()
    {
        Ticket ticket = new Ticket("Log4j-Lücke", "Webserver betroffen", DateTimeOffset.UtcNow);
        ticket.Escalate(3);

        Assert.Throws<ArgumentException>(() => ticket.Escalate(2));
    }
    [Fact]
    public void Close_sets_status_and_closed_at()
    {
        Ticket ticket = new Ticket("Close Ticket", "test", DateTimeOffset.UtcNow);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        ticket.Close(now);

        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.Equal(now, ticket.ClosedAt);
    }
    [Fact]
    public void Second_close_keeps_first_closed_at()
    {
        Ticket ticket = new Ticket("Log4j-Lücke", "Webserver betroffen", DateTimeOffset.UtcNow);
        DateTimeOffset first = DateTimeOffset.UtcNow;
        DateTimeOffset second = first.AddHours(1);

        ticket.Close(first);
        ticket.Close(second);

        Assert.Equal(first, ticket.ClosedAt);
    }

    [Fact]
    public void Assign_sets_assignee_and_in_progress()
    {
        Ticket ticket = new Ticket("Log4j-Lücke", "Webserver betroffen", DateTimeOffset.UtcNow);
        Guid userId = Guid.NewGuid();

        ticket.Assign(userId, "Anna");

        Assert.Equal(userId, ticket.AssigneeId);
        Assert.Equal("Anna", ticket.AssigneeName);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
    }

    [Fact]
    public void Assign_on_closed_ticket_throws()
    {
        Ticket ticket = new Ticket("Log4j-Lücke", "Webserver betroffen", DateTimeOffset.UtcNow);
        ticket.Close(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => ticket.Assign(Guid.NewGuid(), "Anna"));
    }

}