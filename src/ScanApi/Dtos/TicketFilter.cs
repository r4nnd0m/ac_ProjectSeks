using System.ComponentModel.DataAnnotations;
using Sims.Shared.Enums;

namespace ScanApi.Dtos;

public record TicketFilter(
    TicketStatus? Status = null,
    Severity? Severity = null,
    bool AssignedToMe = false,
    [property: Range(1, int.MaxValue)] int Page = 1,
    [property: Range(1, 100)] int PageSize = 20);