using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Sims.Logging;

public static class RedisLoggingExtensions
{
    public static ILoggingBuilder AddRedis(this ILoggingBuilder builder,
        IConnectionMultiplexer multiplexer, string service)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddProvider(new RedisLoggerProvider(multiplexer, service));
    }
}
