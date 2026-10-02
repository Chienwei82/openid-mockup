using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.Users;

namespace OidcMock.Core.Grants;

/// <summary>
/// Peticion del token endpoint ya autenticada como cliente y deserializada. Cada grant decide que
/// campos necesita; los que no usa se ignoran. <paramref name="ScopesRequested"/> distingue una
/// peticion sin scope de una que pide exactamente los scopes que trae: el grant refresh_token lo
/// necesita, porque sin scope debe conservar el concedido y con scope solo puede reducirlo.
/// </summary>
public sealed record TokenRequest(
    Client Client,
    string Issuer,
    IReadOnlyList<string> Scopes,
    string? Code,
    string? RedirectUri,
    string? CodeVerifier,
    string? RefreshToken,
    string? UserName,
    string? Password,
    string? DeviceCode = null,
    bool ScopesRequested = true);

/// <summary>
/// Estrategia de un grant type del token endpoint. Cada grant sabe autenticarse por su cuenta y
/// emitir sus tokens; el endpoint no conoce los casos particulars de ninguno.
/// </summary>
public interface IGrantHandler
{
    /// <summary>Nombre del grant, tal como viaja en el parametro grant_type.</summary>
    string GrantType { get; }

    /// <summary>Grants que no emiten id_token (client_credentials) lo declaran aqui.</summary>
    bool IssuesIdToken { get; }

    Task<Result<TokenResponse>> HandleAsync(TokenRequest request);
}