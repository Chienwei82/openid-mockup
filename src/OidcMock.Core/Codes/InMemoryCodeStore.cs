using System.Collections.Concurrent;
using OidcMock.Core.Errors;

namespace OidcMock.Core.Codes;

/// <summary>
/// Store de codigos de autorizacion en memoria. Como el mock es de un solo proceso y sin base de
/// datos, un diccionario concurrente basta; el reloj llega inyectado para que los tests puedan
/// simular expiraciones sin Thread.Sleep.
/// </summary>
public sealed class InMemoryCodeStore : ICodeStore
{
    private const string RedeemedOrUnknownCode = "El codigo de autorizacion es invalido, ya fue usado o caduco.";

    private readonly ConcurrentDictionary<string, AuthorizationCode> _codes = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public InMemoryCodeStore(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
    }

    public AuthorizationCode Issue(AuthorizationCodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Expire();

        var issuedAt = _timeProvider.GetUtcNow();
        var code = new AuthorizationCode(
            OpaqueToken.New(),
            request.ClientId,
            request.UserName,
            request.Scopes,
            request.Nonce,
            request.State,
            request.CodeChallenge,
            request.CodeChallengeMethod,
            issuedAt,
            issuedAt + request.Lifetime,
            request.RedirectUri);

        _codes[code.Code] = code;

        return code;
    }

    public Result<AuthorizationCode> Redeem(string code)
    {
        if (string.IsNullOrEmpty(code) || !_codes.TryRemove(code, out var authorizationCode))
        {
            return Result<AuthorizationCode>.Fail(ProtocolErrors.InvalidGrant(RedeemedOrUnknownCode));
        }

        return authorizationCode.IsExpiredAt(_timeProvider.GetUtcNow())
            ? Result<AuthorizationCode>.Fail(ProtocolErrors.InvalidGrant(RedeemedOrUnknownCode))
            : Result<AuthorizationCode>.Ok(authorizationCode);
    }

    public void Expire()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var expired in _codes.Where(entry => entry.Value.IsExpiredAt(now)).Select(entry => entry.Key))
        {
            _codes.TryRemove(expired, out _);
        }
    }

    /// <summary>
    /// Codigos vigentes. Depura los caducados antes de responder, para que List() refleje el estado
    /// efectivo del store y este no crezca si el mock lleva rato emitiendo codigos.
    /// </summary>
    public IReadOnlyList<AuthorizationCode> List()
    {
        Expire();

        return [.. _codes.Values];
    }
}