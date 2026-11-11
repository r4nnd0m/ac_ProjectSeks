using System.ComponentModel.DataAnnotations;

namespace AuthApi.Dtos;

public record CreateUserRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    [Required] string Role,
    [Required] Guid TeamId);
    

