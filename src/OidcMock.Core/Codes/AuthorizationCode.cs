namespace OidcMock.Core.Codes;

/// <summary>
/// Codigo de autorizacion emitido por el endpoint /connect/authorize, pendiente de canjear por un
/// token. Es de un solo uso y expira; el estado vive en memoria, nunca en disco.
/// </summary>
public sealed record AuthorizationCode(
    string Code,
    string ClientId,
    string UserName,
    IReadOnlyList<string> Scopes,
    string? Nonce,
    string? State,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    DateTimeOffset AuthenticatedAt,
    DateTimeOffset ExpiresAt,
    string? RedirectUri = null)
{
    public bool IsExpiredAt(DateTimeOffset instant) => instant >= ExpiresAt;

    public bool RequiresPkce => !string.IsNullOrEmpty(CodeChallenge);
}