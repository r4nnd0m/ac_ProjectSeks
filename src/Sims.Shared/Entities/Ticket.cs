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
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public string? AssigneeName { get; private set; }

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

    public void Close(DateTimeOffset now)
    {
        if (IsFinished)
        {
            return;
        }

        Status = TicketStatus.Closed;
        ClosedAt = now;
    }

    public void Escalate(int newLevel)
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("Ticket ist abgeschlossen.");
        }

        if (newLevel <= EscalationLevel)
        {
            throw new ArgumentException("Die Stufe darf nur steigen.", nameof(newLevel));
        }

        EscalationLevel = newLevel;
        Status = TicketStatus.Escalated;
    }   
    public void Assign(Guid userId, string userName)
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("Ticket ist abgeschlossen.");
        }

        AssigneeId = userId;
        AssigneeName = userName;
        Status = TicketStatus.InProgress;
    }

}


