namespace AuthApi.Dtos;

public record MfaVerifyRequest(string MfaToken, string Code);
