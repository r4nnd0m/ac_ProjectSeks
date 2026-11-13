using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Sims.Logging;

public sealed class RedisLoggerProvider : ILoggerProvider
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly string _serviceName;

    public RedisLoggerProvider(IConnectionMultiplexer multiplexer, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        _multiplexer = multiplexer;
        _serviceName = serviceName;
    }

    public ILogger CreateLogger(string categoryName) => new RedisLogger(_multiplexer, _serviceName);

    // Die Redis-Verbindung gehört der Anwendung und wird dort geschlossen.
    public void Dispose()
    {
    }
}
