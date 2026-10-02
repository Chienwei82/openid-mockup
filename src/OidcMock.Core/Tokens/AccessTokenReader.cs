using OidcMock.Core.Claims;
using OidcMock.Core.Errors;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Valida un access token emitido por el mock con la clave que publica su propio JWKS. Los endpoints que
/// necesitan leer el token (userinfo, introspect, revocation) lo hacen por aqui en vez de deserializarlo
/// a mano, para que la validacion de firma y audiencia no se disperse.
/// </summary>
public sealed class AccessTokenReader(SignedTokenValidator validator) : IAccessTokenReader
{
    private const string InvalidTokenDescription =
        "El access token es invalido, ha caducado o no fue emitido por este mock.";

    public Result<AccessTokenClaims> Read(string accessToken, string issuer, IEnumerable<string>? audiences)
    {
        var claims = validator.Validate(
            accessToken,
            issuer,
            audiences,
            ProtocolErrors.InvalidToken(InvalidTokenDescription));

        return claims.Succeeded
            ? Result<AccessTokenClaims>.Ok(ToClaims(claims.Value!))
            : Result<AccessTokenClaims>.Fail(claims.Error!);
    }

    /// <summary>
    /// El access token siempre lleva <c>scope</c>, asi que un token validado sin el es de otro tipo y
    /// no se puede describir como access token del mock.
    /// </summary>
    private static AccessTokenClaims ToClaims(IDictionary<string, object> claims)
    {
        var scopes = TokenClaimReader.Read(claims, ProtocolClaimNames.Scope)
            ?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            ?? [];

        return new AccessTokenClaims(
            TokenClaimReader.ReadRequired(claims, ProtocolClaimNames.Subject),
            TokenClaimReader.ReadRequired(claims, ProtocolClaimNames.ClientId),
            scopes,
            TokenClaimReader.ReadRequired(claims, ProtocolClaimNames.TokenId),
            TokenClaimReader.ReadExpiry(claims));
    }
}