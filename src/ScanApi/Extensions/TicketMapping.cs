using Sims.Shared.Entities;
using ScanApi.Dtos;

namespace ScanApi.Extensions;

public static class TicketMapping
{
    public static TicketResponse ToResponse(this Ticket ticket) =>
        new TicketResponse(
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.Severity,
            ticket.Status,
            ticket.Cve,
            ticket.System,
            ticket.ProjectId,
            ticket.ReporterName,
            ticket.AssigneeName,
            ticket.AssignedRole,
            ticket.EscalationLevel,
            ticket.DueAt,
            ticket.CreatedAt,
            ticket.ClosedAt);
}