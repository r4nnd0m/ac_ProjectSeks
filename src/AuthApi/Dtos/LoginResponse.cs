namespace AuthApi.Dtos;

public record LoginResponse(string? AccessToken, int? ExpiresInSeconds, bool MfaRequired, string? MfaToken);
