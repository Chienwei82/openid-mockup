using System.Collections.Concurrent;
using OidcMock.Core.Codes;
using OidcMock.Core.Errors;

namespace OidcMock.Core.Grants;

/// <summary>
/// Store de refresh tokens en memoria. Cada canje consume el token y el endpoint emite uno nuevo,
/// que es la rotacion que espera un cliente OAuth y limita el valor de un token robado.
/// </summary>
public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private const string InvalidRefreshToken =
        "El refresh token es invalido, ya fue usado, fue revocado o caduco.";

    private readonly ConcurrentDictionary<string, RefreshToken> _tokens = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public InMemoryRefreshTokenStore(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
    }

    public RefreshToken Issue(RefreshTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Expire();

        var issuedAt = _timeProvider.GetUtcNow();
        var refreshToken = new RefreshToken(
            OpaqueToken.New(),
            request.ClientId,
            request.Subject,
            request.Scopes,
            issuedAt,
            issuedAt + request.Lifetime);

        _tokens[refreshToken.Token] = refreshToken;

        return refreshToken;
    }

    public Result<RefreshToken> Redeem(string token)
    {
        if (string.IsNullOrEmpty(token) || !_tokens.TryRemove(token, out var refreshToken))
        {
            return Result<RefreshToken>.Fail(ProtocolErrors.InvalidGrant(InvalidRefreshToken));
        }

        return refreshToken.IsExpiredAt(_timeProvider.GetUtcNow())
            ? Result<RefreshToken>.Fail(ProtocolErrors.InvalidGrant(InvalidRefreshToken))
            : Result<RefreshToken>.Ok(refreshToken);
    }

    public void Revoke(string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            _tokens.TryRemove(token, out _);
        }
    }

    public void Expire()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var expired in _tokens.Where(entry => entry.Value.IsExpiredAt(now)).Select(entry => entry.Key))
        {
            _tokens.TryRemove(expired, out _);
        }
    }

    public IReadOnlyList<RefreshToken> List()
    {
        Expire();

        return [.. _tokens.Values];
    }
}