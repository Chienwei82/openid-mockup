using System.Collections.Concurrent;
using OidcMock.Core.Codes;
using OidcMock.Core.Errors;

namespace OidcMock.Core.Grants;

/// <summary>
/// Store de refresh tokens en memoria. Cada canje consume el token y el endpoint emite uno nuevo de
/// la misma familia, que es la rotacion que espera un cliente OAuth. Los tokens ya canjeados se
/// recuerdan: si uno vuelve a presentarse, se revoca la familia entera, porque un token robado que
/// se reutiliza delata a alguien que copio una cadena de refreshes.
/// </summary>
public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private const string InvalidRefreshToken =
        "El refresh token es invalido, ya fue usado, fue revocado o caduco.";

    private const string ReusedRefreshToken =
        "El refresh token ya fue canjeado. Se revoco toda la familia por posible reutilizacion.";

    private readonly ConcurrentDictionary<string, RefreshToken> _tokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RotatedToken> _rotated = new(StringComparer.Ordinal);
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
            issuedAt + request.Lifetime,
            request.FamilyId ?? OpaqueToken.New());

        _tokens[refreshToken.Token] = refreshToken;

        return refreshToken;
    }

    public Result<RefreshToken> Redeem(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Fail(InvalidRefreshToken);
        }

        if (_rotated.TryGetValue(token, out var reused))
        {
            RevokeFamily(reused.FamilyId);

            return Fail(ReusedRefreshToken);
        }

        if (!_tokens.TryRemove(token, out var refreshToken))
        {
            return Fail(InvalidRefreshToken);
        }

        _rotated[token] = new RotatedToken(refreshToken.FamilyId, refreshToken.ExpiresAt);

        return refreshToken.IsExpiredAt(_timeProvider.GetUtcNow())
            ? Fail(InvalidRefreshToken)
            : Result<RefreshToken>.Ok(refreshToken);
    }

    public void Revoke(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        _tokens.TryRemove(token, out _);
        _rotated.TryRemove(token, out _);
    }

    public void RevokeFamily(string familyId)
    {
        if (string.IsNullOrEmpty(familyId))
        {
            return;
        }

        foreach (var member in _tokens.Where(entry => entry.Value.FamilyId == familyId).Select(entry => entry.Key))
        {
            _tokens.TryRemove(member, out _);
        }

        foreach (var member in _rotated.Where(entry => entry.Value.FamilyId == familyId).Select(entry => entry.Key))
        {
            _rotated.TryRemove(member, out _);
        }
    }

    public void Expire()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var expired in _tokens.Where(entry => entry.Value.IsExpiredAt(now)).Select(entry => entry.Key))
        {
            _tokens.TryRemove(expired, out _);
        }

        foreach (var forgotten in _rotated.Where(entry => entry.Value.ExpiresAt <= now).Select(entry => entry.Key))
        {
            _rotated.TryRemove(forgotten, out _);
        }
    }

    public IReadOnlyList<RefreshToken> List()
    {
        Expire();

        return [.. _tokens.Values];
    }

    private static Result<RefreshToken> Fail(string description) =>
        Result<RefreshToken>.Fail(ProtocolErrors.InvalidGrant(description));

    /// <summary>
    /// Recuerdo de un token ya canjeado: que familia revocation y hasta cuando se recuerda, para no
    /// crecer sin limite.
    /// </summary>
    private sealed record RotatedToken(string FamilyId, DateTimeOffset ExpiresAt);
}