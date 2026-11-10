using ScanApi.Dtos;

namespace ScanApi.Services;

public enum TicketOutcome
{
    Ok,
    NotFound,
    Conflict
}

public record TicketResult(TicketOutcome Outcome, TicketResponse? Ticket = null);