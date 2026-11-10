using StackExchange.Redis;

namespace AuthApi.Services;

public class RedisTokenBlacklist : ITokenBlacklist
{
    private const string Prefix = "auth:blacklist:";
    private readonly IDatabase _db;

    public RedisTokenBlacklist(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task RevokeAsync(string jti, DateTimeOffset expiresAt)
    {
        TimeSpan ttl = expiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        await _db.StringSetAsync(Prefix + jti, "1", ttl);
    }

    public async Task<bool> IsRevokedAsync(string jti)
    {
        return await _db.KeyExistsAsync(Prefix + jti);
    }
}