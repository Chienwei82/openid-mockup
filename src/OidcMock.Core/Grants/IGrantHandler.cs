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
    bool ScopesRequested = true)
{
    /// <summary>
    /// Peticion de un grant que no aporta ningun secreto de por si, solo el cliente y los scopes
    /// concedidos: client_credentials y, en general, cualquier grant sin credencial propia.
    ///
    /// El constructor posicional obliga a_<c>null</c> en seis slots para llegar al septimo, y con
    /// once parametros del mismo tipo, transponer dos contiguos compila sin avisar (D-044).
    /// Las factorias de esta clase son la forma legible de construirla: cada grant pide lo que
    /// necesita por nombre.
    /// </summary>
    public static TokenRequest ForClient(Client client, string issuer, IReadOnlyList<string> scopes) =>
        new(client, issuer, scopes, null, null, null, null, null, null);

    /// <summary>Peticion del grant authorization_code, con su codigo y la verificacion de PKCE.</summary>
    public static TokenRequest ForAuthorizationCode(
        Client client,
        string issuer,
        IReadOnlyList<string> scopes,
        string? code,
        string? redirectUri,
        string? codeVerifier) =>
        new(client, issuer, scopes, code, redirectUri, codeVerifier, null, null, null);

    /// <summary>Peticion del grant password, con las credenciales del recurso.</summary>
    public static TokenRequest ForPassword(
        Client client,
        string issuer,
        IReadOnlyList<string> scopes,
        string? userName,
        string? password) =>
        new(client, issuer, scopes, null, null, null, null, userName, password);

    /// <summary>
    /// Peticion del grant refresh_token. <paramref name="scopesRequested"/> es lo que el cliente
    /// pidio, no lo concedido: sin scopes en la peticion la marca es false y el grant conserva
    /// originales en lugar de aplicar narrowing sobre una lista vacia.
    /// </summary>
    public static TokenRequest ForRefreshToken(
        Client client,
        string issuer,
        IReadOnlyList<string> scopes,
        string? refreshToken,
        bool scopesRequested) =>
        new(client, issuer, scopes, null, null, null, refreshToken, null, null, null, scopesRequested);

    /// <summary>
    /// Peticion de un grant por sondeo (device_code y CIBA). El handle viaja en el slot
    /// <c>DeviceCode</c> en ambos casos: para device es el <c>device_code</c> de RFC 8628 y para CIBA
    /// el <c>auth_req_id</c> que devolvio <c>/connect/ciba</c>. Es el mismo parametro del token
    /// endpoint en ambos flujos, que es lo que los une aqui.
    /// </summary>
    public static TokenRequest ForPoll(
        Client client,
        string issuer,
        IReadOnlyList<string> scopes,
        string? handle) =>
        new(client, issuer, scopes, null, null, null, null, null, null, handle);
}

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