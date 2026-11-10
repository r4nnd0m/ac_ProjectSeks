namespace AuthApi.Services;

public interface ITokenBlacklist
{
    Task RevokeAsync(string jti, DateTimeOffset expiresAt);
    Task<bool> IsRevokedAsync(string jti);
}