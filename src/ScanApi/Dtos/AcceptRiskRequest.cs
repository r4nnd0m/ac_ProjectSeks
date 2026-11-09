using System.ComponentModel.DataAnnotations;

namespace ScanApi.Dtos;

public record AcceptRiskRequest(
    [property: Required, StringLength(2000, MinimumLength = 20)] string Justification);