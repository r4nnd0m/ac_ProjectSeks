using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Sims.Logging;

/// <summary>Stellt Redis-Logger für die ILogger-Kategorien einer Anwendung bereit.</summary>
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

    public void Dispose()
    {
        // Die aufrufende Anwendung besitzt die geteilte Redis-Verbindung.
    }
}
