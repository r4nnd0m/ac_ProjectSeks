using System.ComponentModel.DataAnnotations;
using Sims.Shared.Enums;

namespace ScanApi.Dtos;

public record UpdateTicketRequest(
    [Required, StringLength(200)] string Title,
    [StringLength(4000)] string Description,
    Severity Severity);