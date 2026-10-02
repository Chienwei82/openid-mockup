using OidcMock.Core.Errors;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Claims que el mock necesita del id_token que llega como <c>id_token_hint</c> en /connect/endsession:
/// quien es el usuario y a que cliente se emitio. El <c>sid</c> lo emite el proveedor, no el cliente.
/// </summary>
public sealed record IdTokenClaims(string Subject, string ClientId, string? SessionId);

/// <summary>
/// Lee y valida un id_token emitido por el mock. A diferencia del access token, aqui la audiencia es
/// justamente el <c>client_id</c> que se quiere averiguar, asi que se validan firma, issuer y vigencia
/// y la audiencia se devuelve sin comprobar.
/// </summary>
public interface IIdTokenReader
{
    Result<IdTokenClaims> Read(string idToken, string issuer);
}