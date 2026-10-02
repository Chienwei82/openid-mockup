using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;

namespace OidcMock.Core.Grants;

/// <summary>
/// Peticion del token endpoint tal como llega, con el cliente todavia sin autenticar.
/// </summary>
public sealed record TokenEndpointRequest(
    ClientCredentials Credentials,
    string? GrantType,
    IReadOnlyList<string> Scopes,
    string? Code,
    string? RedirectUri,
    string? CodeVerifier,
    string? RefreshToken,
    string? UserName,
    string? Password,
    string? DeviceCode = null);

/// <summary>
/// Autentica al cliente del token endpoint (client_secret_basic y client_secret_post) y despacha la
/// peticion al handler del grant solicitado.
/// </summary>
public interface ITokenEndpointService
{
    Task<Result<TokenResponse>> IssueTokenAsync(TokenEndpointRequest request, string issuer);
}