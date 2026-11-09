using System.Security.Claims;
using Sims.Shared.Entities;

namespace ScanApi.Extensions;

public static class TicketQueryExtensions
{
    public static IQueryable<Ticket> ForUser(this IQueryable<Ticket> tickets, ClaimsPrincipal user)
    {
        if (user.IsAdmin())
        {
            return tickets;
        }

        Guid teamId = user.GetTeamId();
        return tickets.Where(t => t.TeamId == teamId);
    }
}