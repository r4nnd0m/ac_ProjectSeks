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
    public void Close_sets_status_to_closed()
    {

        Ticket ticket = new Ticket("Close Ticket", "test", DateTimeOffset.UtcNow);

        ticket.Close();

        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }
    [Fact]
    public void Escalate_raises_level_and_sets_status()
    {
        Ticket ticket = new Ticket("Log4j-Lücke", "Webserver betroffen", DateTimeOffset.UtcNow);

        ticket.Escalate(2);

        Assert.Equal(2, ticket.EscalationLevel);
        Assert.Equal(TicketStatus.Escalated, ticket.Status);
    }
}