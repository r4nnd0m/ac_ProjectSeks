// using Microsoft.Extensions.Logging;

// namespace Sims.Worker.Demo;

// // Synchrone Demo-Ausgabe: Erklärung und Logzeile bleiben in derselben Reihenfolge.
// // Der normale Worker verwendet weiterhin den vorhandenen Console-/Redis-Logger.
// internal sealed class DemoLoggerProvider : ILoggerProvider
// {
//     public ILogger CreateLogger(string categoryName) => new DemoLogger();
//     public void Dispose() { }

//     private sealed class DemoLogger : ILogger
//     {
//         public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
//         public bool IsEnabled(LogLevel level) => level >= LogLevel.Information && level != LogLevel.None;
//         public void Log<TState>(LogLevel level, EventId eventId, TState state,
//             Exception? exception, Func<TState, Exception?, string> formatter)
//         {
//             if (IsEnabled(level)) Console.WriteLine($"  [DEMO/{level}] {formatter(state, exception)}");
//         }
//     }
// }
