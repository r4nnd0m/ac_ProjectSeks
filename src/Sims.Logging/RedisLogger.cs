using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Sims.Logging;

/// <summary>Schreibt technische Logmeldungen in die gemeinsame Redis-Liste.</summary>
public sealed class RedisLogger : ILogger
{
    public const string RedisKey = "logs";
    public const int MaxEntries = 10_000;

    private readonly string _serviceName;
    private readonly Action<string> _writeRecord;

    public RedisLogger(IConnectionMultiplexer multiplexer, string serviceName)
        : this(serviceName, json =>
        {
            // Bei einem Ausfall bleiben die anderen Logger (z.B. Konsole) nutzbar.
            if (!multiplexer.IsConnected)
                return;

            var database = multiplexer.GetDatabase();
            database.ListLeftPush(RedisKey, json, flags: CommandFlags.FireAndForget);
            database.ListTrim(RedisKey, 0, MaxEntries - 1, flags: CommandFlags.FireAndForget);
        })
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
    }

    // Trennt das Nachrichtenformat vom Transport, damit es ohne Redis testbar ist.
    internal RedisLogger(string serviceName, Action<string> writeRecord)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(writeRecord);
        _serviceName = serviceName;
        _writeRecord = writeRecord;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel >= LogLevel.Information && logLevel < LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        try
        {
            var message = formatter(state, exception);
            if (exception is not null)
            {
                // Keine Exception.Message und keinen Stacktrace automatisch übernehmen.
                message = string.IsNullOrEmpty(message)
                    ? exception.GetType().Name
                    : $"{message} ({exception.GetType().Name})";
            }

            if (string.IsNullOrEmpty(message))
                return;

            var json = JsonSerializer.Serialize(new
            {
                id = Guid.NewGuid(),
                timestamp = DateTimeOffset.UtcNow,
                level = logLevel.ToString(),
                service = _serviceName,
                message
            });

            _writeRecord(json);
        }
        catch (Exception)
        {
            // Logging darf die Anwendung nicht stoppen. Hier nicht erneut loggen,
            // sonst könnte ein Logger-Fehler eine Endlosschleife auslösen.
        }
    }
}
