using System.ComponentModel.DataAnnotations;

namespace AuthApi.Dtos;

public record CreateTeamRequest(
    [property: Required, StringLength(100)] string Name);