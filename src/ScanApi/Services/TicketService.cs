using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ScanApi.Dtos;
using ScanApi.Extensions;
using Sims.Shared.Audit;
using Sims.Shared.Data;
using Sims.Shared.Escalation;
using Sims.Shared.Entities;

namespace ScanApi.Services;

public interface ITicketService
{
    Task<PagedResponse<TicketResponse>> ListAsync(TicketFilter filter, ClaimsPrincipal user, CancellationToken ct = default);

    Task<TicketResponse?> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);

    Task<TicketResponse?> CreateAsync(CreateTicketRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    Task<TicketResult> UpdateAsync(Guid id, UpdateTicketRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    Task<TicketOutcome> TakeAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);

    Task<TicketOutcome> CloseAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);

    Task<TicketOutcome> AcceptRiskAsync(Guid id, AcceptRiskRequest request, ClaimsPrincipal user, CancellationToken ct = default);
}

public class TicketService(SimsDbContext db, TimeProvider clock, AuditService audit, EscalationPolicy escalation) : ITicketService
{
    public async Task<PagedResponse<TicketResponse>> ListAsync(TicketFilter filter, ClaimsPrincipal user, CancellationToken ct = default)
    {
        Guid userId = user.GetUserId();
        IQueryable<Ticket> query = db.Tickets.ForUser(user).AsNoTracking();

        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (filter.Severity is { } severity)
        {
            query = query.Where(t => t.Severity == severity);
        }

        if (filter.AssignedToMe)
        {
            query = query.Where(t => t.AssigneeId == userId);
        }

        int total = await query.CountAsync(ct);
        List<Ticket> tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        List<TicketResponse> items = tickets.Select(t => t.ToResponse()).ToList();
        return new PagedResponse<TicketResponse>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<TicketResponse?> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        Ticket? ticket = await db.Tickets.ForUser(user).AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        return ticket?.ToResponse();
    }

    public async Task<TicketResponse?> CreateAsync(CreateTicketRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        Guid teamId = user.GetTeamId();

        if (request.ProjectId is { } projectId)
        {
            bool isAdmin = user.IsAdmin();
            Project? project = await db.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId && (isAdmin || p.TeamId == teamId), ct);

            if (project is null)
            {
                return null;
            }

            teamId = project.TeamId;
        }

        var now = clock.GetUtcNow();
        var start = escalation.StartFor(request.Severity, isKev: false, now);
        NewTicket data = new NewTicket(
            teamId,
            request.Title,
            request.Description,
            request.Severity,
            user.GetUserId(),
            user.GetUserName(),
            ProjectId: request.ProjectId,
            Cve: request.Cve,
            System: request.System,
            EscalationLevel: start.Level,
            AssignedRole: start.AssignedRole,
            DueAt: start.DueAt);

        Ticket ticket = new Ticket(data, now);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        return ticket.ToResponse();
    }

    public async Task<TicketResult> UpdateAsync(Guid id, UpdateTicketRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        Ticket? ticket = await FindAsync(id, user, ct);

        if (ticket is null)
        {
            return new TicketResult(TicketOutcome.NotFound);
        }

        if (ticket.IsFinished)
        {
            return new TicketResult(TicketOutcome.Conflict);
        }

        ticket.Update(request.Title, request.Description, request.Severity);
        await db.SaveChangesAsync(ct);

        return new TicketResult(TicketOutcome.Ok, ticket.ToResponse());
    }

    public async Task<TicketOutcome> TakeAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        Ticket? ticket = await FindAsync(id, user, ct);

        if (ticket is null)
        {
            return TicketOutcome.NotFound;
        }

        if (ticket.IsFinished)
        {
            return TicketOutcome.Conflict;
        }

        ticket.Assign(user.GetUserId(), user.GetUserName());
        audit.Record(user.GetUserId(), user.GetUserName(), AuditActions.TicketTaken, ticket.Id, null);
        await db.SaveChangesAsync(ct);

        return TicketOutcome.Ok;
    }

    public async Task<TicketOutcome> CloseAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        Ticket? ticket = await FindAsync(id, user, ct);

        if (ticket is null)
        {
            return TicketOutcome.NotFound;
        }

        if (ticket.IsFinished)
        {
            return TicketOutcome.Conflict;
        }

        ticket.Close(clock.GetUtcNow());
        audit.Record(user.GetUserId(), user.GetUserName(), AuditActions.TicketClosed, ticket.Id, null);
        await db.SaveChangesAsync(ct);

        return TicketOutcome.Ok;
    }

    public async Task<TicketOutcome> AcceptRiskAsync(Guid id, AcceptRiskRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        Ticket? ticket = await FindAsync(id, user, ct);

        if (ticket is null)
        {
            return TicketOutcome.NotFound;
        }

        if (ticket.IsFinished)
        {
            return TicketOutcome.Conflict;
        }

        ticket.AcceptRisk(clock.GetUtcNow());
        audit.Record(user.GetUserId(), user.GetUserName(), AuditActions.RiskAccepted, ticket.Id, request.Justification);
        await db.SaveChangesAsync(ct);

        return TicketOutcome.Ok;
    }

    private async Task<Ticket?> FindAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        return await db.Tickets.ForUser(user).FirstOrDefaultAsync(t => t.Id == id, ct);
    }
}