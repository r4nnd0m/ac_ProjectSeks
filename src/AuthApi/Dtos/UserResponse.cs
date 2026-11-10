using System;
using System.ComponentModel.DataAnnotations;


namespace AuthApi.Dtos;

public record UserResponse(
    Guid Id,
    string Email,
    string Role,
    Guid TeamId,
    bool IsActive,
    bool MfaEnabled
    );


