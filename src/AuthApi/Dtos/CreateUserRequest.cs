using System.ComponentModel.DataAnnotations;

namespace AuthApi.Dtos;

public record CreateUserRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password,
    [property: Required] string Role,
    [property: Required] Guid TeamId);
    

