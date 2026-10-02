using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OidcMock.IntegrationTests.Composition;

/// <summary>
/// Captura lo que el mock escribe en el log. Lo que se registra son metadatos (cliente, grant,
/// caducidad); los tokens y los secretos no, porque un log de desarrollo es texto que acaba en
/// consolas, en el pipe de CI y en archivos compartidos.
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<RecordedLog> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);

    public void Dispose()
    {
        // Sin recursos: el provider solo acumula texto en memoria.
    }

    public bool Mentions(string value) =>
        Entries.Any(entry => entry.Message.Contains(value, StringComparison.Ordinal)
            || entry.State?.Contains(value, StringComparison.Ordinal) == true);

    public RecordedLog? OnlyIn(string category) =>
        In(category).Take(2).ToArray() is [var found, _] ? found : null;

    public IEnumerable<RecordedLog> In(string category) => Entries.Where(entry => entry.Category == category);

    /// <summary>
    /// Entradas del propio mock, sin las del framework. ASP.NET registra el RedirectResult del
    /// authorize con la URL completa, que lleva el codigo dentro: es comportamiento del framework y
    /// lo que se comprueba aqui es que los eventos de OidcMock no lo vuelquen.
    /// </summary>
    public IEnumerable<RecordedLog> OwnEntries =>
        Entries.Where(entry => !entry.Category.StartsWith("Microsoft.", StringComparison.Ordinal));

    /// <summary>Si alguno de los eventos del propio mock menciona un valor concreto.</summary>
    public bool OwnLogsMention(string value) =>
        OwnEntries.Any(entry => entry.Message.Contains(value, StringComparison.Ordinal)
            || entry.State?.Contains(value, StringComparison.Ordinal) == true);

    private sealed class RecordingLogger(string category, ConcurrentQueue<RecordedLog> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new RecordedLog(
                category,
                logLevel,
                eventId.Id,
                formatter(state, exception),
                state?.ToString()));
        }
    }
}

public sealed record RecordedLog(string Category, LogLevel Level, int EventId, string Message, string? State);