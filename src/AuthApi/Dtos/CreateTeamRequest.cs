using System.ComponentModel.DataAnnotations;

namespace AuthApi.Dtos;

public record CreateTeamRequest(
    [Required, StringLength(100)] string Name);