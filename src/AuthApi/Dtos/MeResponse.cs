namespace AuthApi.Dtos;

public record MeResponse(Guid Id, string Email, string Role, Guid TeamId, bool MfaEnabled);
