using System;
using System.Collections.Generic;
using System.Text;

public class Ticket
{
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    public int EscalationLevel { get; private set; } = 1;

    // Konstruktor:
    // Setzt Title, Description, CreatedAt einmal beim Erzeugen
    // Danach nicht mehr änderbar (private set), damit niemand die Angaben nachträglich verändern kann und das Audit-Log integer bleibt.

    public Ticket(string title, string description, DateTimeOffset createdAt)
    {
        Title = title;
        Description = description;
        CreatedAt = createdAt;
    }

    public bool IsFinished => Status == TicketStatus.Closed || Status == TicketStatus.RiskAccepted;

    public void Close()
    {
        if (IsFinished)
        {
            return;
        }

        Status = TicketStatus.Closed;
    }

    public void Escalate(int newLevel)
    {
        if (newLevel <= EscalationLevel)
        {
            return;
        }
        else if (IsFinished)
        {
            return;
        }

        EscalationLevel = newLevel;
        Status = TicketStatus.Escalated;
    }
}


