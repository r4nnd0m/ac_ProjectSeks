using System.ComponentModel.DataAnnotations;
using Sims.Shared.Entities;
using Sims.Shared.Enums;

namespace ScanApi.Dtos;

public record UpdateTicketRequest(
    [Required, StringLength(Ticket.MaxTitleLength)] string Title,
    [StringLength(4000)] string Description,
    [EnumDataType(typeof(Severity))] Severity Severity);