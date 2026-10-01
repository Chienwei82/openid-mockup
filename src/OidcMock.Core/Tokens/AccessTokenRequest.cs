using OidcMock.Core.Users;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Datos necesarios para emitir un access_token. <c>User</c> es null en los flujos sin usuario
/// (por ejemplo, client_credentials), donde solo se emiten los claims de protocolo.
/// </summary>
public sealed record AccessTokenRequest(
    string Issuer,
    string ClientId,
    IReadOnlyList<string> GrantedScopes,
    IReadOnlyList<string> Audiences,
    string Subject,
    User? User,
    DateTimeOffset IssuedAt,
    TimeSpan Lifetime);