using System.Collections.Concurrent;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Consentimientos recordados en memoria: un mock vive mientras dura el proceso y no necesita
/// persistirlos. Lo recordado es el conjunto de scopes aprobados por cliente y usuario, y crece con
/// cada aprobacion; denegar no recuerda nada.
/// </summary>
public sealed class InMemoryConsentStore : IConsentStore
{
    private readonly ConcurrentDictionary<(string ClientId, string UserName), ConcurrentDictionary<string, byte>> _granted = new();

    public void Remember(string clientId, string userName, IReadOnlyList<string> scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentNullException.ThrowIfNull(scopes);

        var approved = _granted.GetOrAdd(
            (clientId, userName),
            _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));

        foreach (var scope in scopes)
        {
            approved.TryAdd(scope, 0);
        }
    }

    public bool IsGranted(string clientId, string userName, IReadOnlyList<string> scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentNullException.ThrowIfNull(scopes);

        return _granted.TryGetValue((clientId, userName), out var approved)
            && scopes.All(approved.ContainsKey);
    }
}