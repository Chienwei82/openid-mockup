using OidcMock.Core.Claims;
using OidcMock.Core.Errors;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Lee el id_token que un cliente envia como <c>id_token_hint</c> al cerrar sesion. Del token solo
/// interesa quien es el usuario y para que cliente se emitio; la firma, el issuer y la vigencia se
/// validan contra la clave que publica el propio JWKS, de modo que un hint manipulado no abre la
/// redireccion de un <c>post_logout_redirect_uri</c>.
/// </summary>
public sealed class IdTokenReader(SignedTokenValidator validator) : IIdTokenReader
{
    private const string InvalidHintDescription =
        "El id_token_hint es invalido, ha caducado o no fue emitido por este mock.";

    public Result<IdTokenClaims> Read(string idToken, string issuer)
    {
        var claims = validator.Validate(
            idToken,
            issuer,
            audiences: null,
            ProtocolErrors.InvalidRequest(InvalidHintDescription));

        return claims.Succeeded
            ? Result<IdTokenClaims>.Ok(ToIdTokenClaims(claims.Value!))
            : Result<IdTokenClaims>.Fail(claims.Error!);
    }

    private static IdTokenClaims ToIdTokenClaims(IDictionary<string, object> claims) =>
        new(
            TokenClaimReader.ReadRequired(claims, ProtocolClaimNames.Subject),
            ReadAudience(claims),
            TokenClaimReader.Read(claims, ProtocolClaimNames.SessionId));

    /// <summary>
    /// <c>aud</c> llega como cadena cuando el token tiene una sola audiencia y como arreglo cuando tiene
    /// varias (OpenID Connect Core 2). Un id_token siempre tiene una, asi que basta con la primera.
    /// </summary>
    private static string ReadAudience(IDictionary<string, object> claims)
    {
        if (!claims.TryGetValue(ProtocolClaimNames.Audience, out var audience))
        {
            return string.Empty;
        }

        return audience is IEnumerable<object> audiences
            ? audiences.OfType<string>().FirstOrDefault() ?? string.Empty
            : audience?.ToString() ?? string.Empty;
    }
}