using Sims.Contracts;
using StackExchange.Redis;

namespace AuthApi.Services;

public class RedisTokenBlacklist : ITokenBlacklist
{
    private readonly IDatabase _db;

    public RedisTokenBlacklist(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task RevokeAsync(string jti, DateTimeOffset expiresAt)
    {
        TimeSpan ttl = expiresAt - DateTimeOffset.UtcNow + AccessTokenPolicy.ClockSkew;
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        await _db.StringSetAsync(AccessTokenPolicy.RevocationKey(jti), "1", ttl);
    }

    public async Task<bool> IsRevokedAsync(string jti)
    {
        return await _db.KeyExistsAsync(AccessTokenPolicy.RevocationKey(jti));
    }
}