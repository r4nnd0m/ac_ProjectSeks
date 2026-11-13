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

        return await query.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => t.ToResponse())
            .ReadPageAsync(filter.Page, filter.PageSize, ct);
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
            Project? project = await db.Projects.AsNoTracking().ForUser(user)
                .FirstOrDefaultAsync(p => p.Id == projectId, ct);

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

    public Task<TicketResult> UpdateAsync(Guid id, UpdateTicketRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        return ChangeAsync(id, user, ticket => ticket.Update(request.Title, request.Description, request.Severity), ct);
    }

    public async Task<TicketOutcome> TakeAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var result = await ChangeAsync(id, user, ticket =>
        {
            var userId = user.GetUserId();
            var name = user.GetUserName();
            ticket.Assign(userId, name);
            audit.Record(userId, name, AuditActions.TicketTaken, id, null);
        }, ct);
        return result.Outcome;
    }

    public async Task<TicketOutcome> CloseAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var result = await ChangeAsync(id, user, ticket =>
        {
            ticket.Close(clock.GetUtcNow());
            audit.Record(user.GetUserId(), user.GetUserName(), AuditActions.TicketClosed, id, null);
        }, ct);
        return result.Outcome;
    }

    public async Task<TicketOutcome> AcceptRiskAsync(Guid id, AcceptRiskRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var result = await ChangeAsync(id, user, ticket =>
        {
            ticket.AcceptRisk(clock.GetUtcNow());
            audit.Record(user.GetUserId(), user.GetUserName(), AuditActions.RiskAccepted, id, request.Justification);
        }, ct);
        return result.Outcome;
    }

    private async Task<TicketResult> ChangeAsync(Guid id, ClaimsPrincipal user, Action<Ticket> change, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Statusprüfung und Audit gehören zur selben Änderung. Auch der Eskalationsjob bearbeitet diese Zeile.
        var ticket = await db.Tickets
            .FromSql($"SELECT * FROM [scan].[Tickets] WITH (XLOCK, ROWLOCK) WHERE [Id] = {id}")
            .ForUser(user)
            .SingleOrDefaultAsync(ct);
        if (ticket is null)
            return new TicketResult(TicketOutcome.NotFound);
        if (ticket.IsFinished)
            return new TicketResult(TicketOutcome.Conflict);

        change(ticket);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new TicketResult(TicketOutcome.Ok, ticket.ToResponse());
    }
}
