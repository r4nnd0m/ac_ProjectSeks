namespace AuthApi.Dtos;

public record MfaSetupResponse(string Secret, string OtpauthUri);
