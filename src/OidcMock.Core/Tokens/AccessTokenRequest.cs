using OidcMock.Core.Users;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Datos necesarios para emitir un access_token. <c>User</c> es null en los flujos sin usuario
/// (por ejemplo, client_credentials), donde solo se emiten los claims de protocolo.
/// <para>
/// No lleva instante de emision: <c>ITokenFactory</c> lo toma del <c>TimeProvider</c> en el momento de
/// firmar, de modo que <c>iat</c>, <c>nbf</c> y <c>exp</c> salen siempre del mismo reloj y ningun
/// llamador puede fijar un instante que contradiga al resto.
/// </para>
/// </summary>
public sealed record AccessTokenRequest(
    string Issuer,
    string ClientId,
    IReadOnlyList<string> GrantedScopes,
    IReadOnlyList<string> Audiences,
    string Subject,
    User? User,
    TimeSpan Lifetime);