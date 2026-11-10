using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sims.Logging.Tests;

public sealed class RedisLoggerTests
{
    [Theory]
    [InlineData(LogLevel.Trace, false)]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Information, true)]
    [InlineData(LogLevel.Warning, true)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    [InlineData(LogLevel.None, false)]
    public void FiltersLogLevelsBeforeFormatting(LogLevel level, bool expected)
    {
        var records = new List<string>();
        var formatted = false;
        var logger = new RedisLogger("worker", records.Add);

        logger.Log(level, new EventId(1), "Nachricht", null, (state, _) =>
        {
            formatted = true;
            return state;
        });

        Assert.Equal(expected, logger.IsEnabled(level));
        Assert.Equal(expected, formatted);
        Assert.Equal(expected ? 1 : 0, records.Count);
    }

    [Fact]
    public void WritesTheSharedJsonContractAndPreservesSpecialCharacters()
    {
        var records = new List<string>();
        var logger = new RedisLogger("scan-api", records.Add);
        const string message = "Prüfung \"Beispiel\"\nzweite Zeile";

        logger.LogInformation("{Message}", message);

        using var json = JsonDocument.Parse(Assert.Single(records));
        var entry = json.RootElement;
        Assert.Equal(new[] { "id", "level", "message", "service", "timestamp" },
            entry.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray());
        Assert.NotEqual(Guid.Empty, entry.GetProperty("id").GetGuid());
        Assert.Equal(TimeSpan.Zero, entry.GetProperty("timestamp").GetDateTimeOffset().Offset);
        Assert.Equal("Information", entry.GetProperty("level").GetString());
        Assert.Equal("scan-api", entry.GetProperty("service").GetString());
        Assert.Equal(message, entry.GetProperty("message").GetString());
    }

    [Fact]
    public void GivesDifferentEntriesDifferentIds()
    {
        var records = new List<string>();
        var logger = new RedisLogger("worker", records.Add);
        logger.LogInformation("Erste Nachricht");
        logger.LogInformation("Zweite Nachricht");
        using var first = JsonDocument.Parse(records[0]);
        using var second = JsonDocument.Parse(records[1]);
        Assert.NotEqual(first.RootElement.GetProperty("id").GetGuid(),
            second.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public void AppendsOnlyTheExceptionType()
    {
        var records = new List<string>();
        var logger = new RedisLogger("worker", records.Add);
        var exception = new InvalidOperationException("private exception detail");

        logger.LogError(exception, "Scan-Durchlauf fehlgeschlagen");

        using var json = JsonDocument.Parse(Assert.Single(records));
        Assert.Equal("Scan-Durchlauf fehlgeschlagen (InvalidOperationException)",
            json.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain(exception.Message, records[0]);
    }

    [Fact]
    public void WriterFailureDoesNotEscapeAndLaterMessagesCanStillBeWritten()
    {
        var records = new List<string>();
        var calls = 0;
        var logger = new RedisLogger("worker", json =>
        {
            if (++calls == 1) throw new IOException("Transport unavailable");
            records.Add(json);
        });

        Assert.Null(Record.Exception(() => logger.LogInformation("Erster Versuch")));
        logger.LogInformation("Zweiter Versuch");
        Assert.Single(records);
    }

    [Fact]
    public void FormatterFailureDoesNotEscapeOrWriteAnIncompleteEntry()
    {
        var records = new List<string>();
        var logger = new RedisLogger("worker", records.Add);

        var error = Record.Exception(() => logger.Log(LogLevel.Information,
            new EventId(1), "state", null, (_, _) => throw new FormatException()));

        Assert.Null(error);
        Assert.Empty(records);
    }

    [Fact]
    public void IgnoresEmptyMessagesWithoutExceptions()
    {
        var records = new List<string>();
        var logger = new RedisLogger("worker", records.Add);
        logger.Log(LogLevel.Information, new EventId(1), "", null, (state, _) => state);
        Assert.Empty(records);
    }
}
