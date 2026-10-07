using OidcMock.Core.Users;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Datos necesarios para emitir un id_token de una peticion de autorizacion o de token.
/// </summary>
public sealed record IdTokenRequest(
    string Issuer,
    string ClientId,
    IReadOnlyList<string> GrantedScopes,
    User User,
    DateTimeOffset AuthenticationTime,
    TimeSpan Lifetime,
    string? Nonce = null,
    string? AccessToken = null,
    string? AuthorizationCode = null,
    string? SessionId = null);