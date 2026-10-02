using System.Collections.Concurrent;
using OidcMock.Core.Codes;
using OidcMock.Core.Configuration;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Store de sesiones en memoria. Como el mock es de un solo proceso y sin base de datos, un
/// diccionario concurrente basta. El reloj llega inyectado para que los tests puedan simular la
/// caducidad sin Thread.Sleep, igual que en el store de codigos.
/// </summary>
public sealed class InMemoryAuthSessionStore(TimeProvider timeProvider, OidcMockOptions options)
    : IAuthSessionStore
{
    private readonly ConcurrentDictionary<string, AuthSession> _sessions = new(StringComparer.Ordinal);

    public AuthSession Start(string userName, string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        Expire();

        var startedAt = timeProvider.GetUtcNow();
        var session = new AuthSession(
            OpaqueToken.New(),
            userName,
            subject,
            startedAt,
            startedAt + options.SessionLifetime);

        _sessions[session.SessionId] = session;

        return session;
    }

    public AuthSession? Find(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || !_sessions.TryGetValue(sessionId, out var session))
        {
            return null;
        }

        if (!session.IsExpiredAt(timeProvider.GetUtcNow()))
        {
            return session;
        }

        _sessions.TryRemove(sessionId, out _);

        return null;
    }

    public void Close(string sessionId)
    {
        if (!string.IsNullOrEmpty(sessionId))
        {
            _sessions.TryRemove(sessionId, out _);
        }
    }

    public void Expire()
    {
        var now = timeProvider.GetUtcNow();

        foreach (var expired in _sessions.Where(entry => entry.Value.IsExpiredAt(now)).Select(entry => entry.Key))
        {
            _sessions.TryRemove(expired, out _);
        }
    }

    public IReadOnlyList<AuthSession> List()
    {
        Expire();

        return [.. _sessions.Values];
    }
}