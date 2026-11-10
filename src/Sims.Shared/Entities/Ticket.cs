using Sims.Shared.Enums;

namespace Sims.Shared.Entities;

public class Ticket
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TeamId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid? FindingId { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public Severity Severity { get; private set; }
    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    public string? Cve { get; private set; }
    public string? System { get; private set; }
    public Guid ReporterId { get; private set; }
    public string ReporterName { get; private set; } = "";
    public Guid? AssigneeId { get; private set; }
    public string? AssigneeName { get; private set; }
    public string? AssignedRole { get; private set; }
    public int EscalationLevel { get; private set; } = 1;
    public DateTimeOffset? DueAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public const int TitleMaxLength = 200;

    public bool IsFinished => Status == TicketStatus.Closed || Status == TicketStatus.RiskAccepted;

    // Nur für EF Core: Die Datenbank braucht einen Konstruktor ohne Parameter, um Tickets zu laden.
    private Ticket()
    {
    }

    // Setzt alle Angaben einmal beim Erzeugen. Danach nur noch über Methoden änderbar (private set),
    // damit niemand die Angaben nachträglich verändern kann und das Audit-Log integer bleibt.
    public Ticket(NewTicket data, DateTimeOffset now)
    {
        TeamId = data.TeamId;
        ProjectId = data.ProjectId;
        FindingId = data.FindingId;
        Title = data.Title;
        Description = data.Description;
        Severity = data.Severity;
        Cve = data.Cve;
        System = data.System;
        ReporterId = data.ReporterId;
        ReporterName = data.ReporterName;
        EscalationLevel = data.EscalationLevel;
        AssignedRole = data.AssignedRole;
        DueAt = data.DueAt;
        CreatedAt = now;
    }

    public void Update(string title, string description, Severity severity)
    {
        EnsureNotFinished();

        Title = title;
        Description = description;
        Severity = severity;
    }

    public void Assign(Guid userId, string userName)
    {
        EnsureNotFinished();

        AssigneeId = userId;
        AssigneeName = userName;
        Status = TicketStatus.InProgress;
    }

    public void Escalate(int newLevel, string assignedRole, DateTimeOffset? newDueAt)
    {
        EnsureNotFinished();

        if (newLevel <= EscalationLevel)
        {
            throw new ArgumentException("Die Stufe darf nur steigen.", nameof(newLevel));
        }

        EscalationLevel = newLevel;
        AssignedRole = assignedRole;
        DueAt = newDueAt;
        Status = TicketStatus.Escalated;
    }

    public void Close(DateTimeOffset now)
    {
        if (IsFinished)
        {
            return;
        }

        Status = TicketStatus.Closed;
        ClosedAt = now;
    }

    public void AcceptRisk(DateTimeOffset now)
    {
        EnsureNotFinished();

        Status = TicketStatus.RiskAccepted;
        ClosedAt = now;
    }

    // Die Wächter-Prüfung steht nur einmal hier, damit sie nicht in jeder Methode wiederholt wird.
    private void EnsureNotFinished()
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("Ticket ist abgeschlossen.");
        }
    }
}