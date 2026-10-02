using System.Collections.Concurrent;

namespace OidcMock.Core.Revocation;

/// <summary>
/// Store de revocaciones en memoria. Los access tokens son JWT sin estado, asi que revocarlos consiste
/// en apuntar su <c>jti</c> aqui; las familias de refresh tokens se apuntan por su identificador, que
/// es lo que permite que una revocacion corte la linea completa de rotaciones.
/// <para>
/// Vive en memoria y no se persiste: al reiniciar el mock todos los tokens emitidos tambien dejan de
/// existir como estado, asi que un registro de revocaciones en disco solo serviria para rechazar tokens
/// que ningun cliente puede volver a presentar.
/// </para>
/// </summary>
public sealed class InMemoryTokenRevocationStore(TimeProvider timeProvider) : ITokenRevocationStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _revokedAccessTokens =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _revokedFamilies =
        new(StringComparer.Ordinal);

    public void RevokeAccessToken(string tokenId, DateTimeOffset expiresAt) =>
        Add(_revokedAccessTokens, tokenId, expiresAt);

    public void RevokeRefreshTokenFamily(string familyId, DateTimeOffset expiresAt) =>
        Add(_revokedFamilies, familyId, expiresAt);

    public bool IsAccessTokenRevoked(string tokenId) => IsRevoked(_revokedAccessTokens, tokenId);

    public bool IsRefreshTokenFamilyRevoked(string familyId) => IsRevoked(_revokedFamilies, familyId);

    public void Expire()
    {
        Expire(_revokedAccessTokens);
        Expire(_revokedFamilies);
    }

    private void Add(ConcurrentDictionary<string, DateTimeOffset> revocations, string id, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        Expire(revocations);
        revocations[id] = expiresAt;
    }

    private bool IsRevoked(ConcurrentDictionary<string, DateTimeOffset> revocations, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !revocations.TryGetValue(id, out var expiresAt))
        {
            return false;
        }

        return expiresAt > timeProvider.GetUtcNow();
    }

    private void Expire(ConcurrentDictionary<string, DateTimeOffset> revocations)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var forgotten in revocations.Where(entry => entry.Value <= now).Select(entry => entry.Key))
        {
            revocations.TryRemove(forgotten, out _);
        }
    }
}