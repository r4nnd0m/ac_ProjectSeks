using AuthApi.Dtos;

namespace AuthApi.Services;

public record UserResult(bool Succeeded, UserResponse? User, string[] Errors);
