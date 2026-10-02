using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OidcMock.Core.Claims;
using OidcMock.Core.Crypto;
using OidcMock.Core.Discovery;
using OidcMock.Core.Errors;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Valida un access token con JsonWebTokenHandler contra el JWKS que publica el propio mock. Los
/// endpoints que necesitan leer el token (userinfo, introspect, revocation) lo hacen por aqui en vez
/// de deserializarlo a mano, para que la validacion de firma y audiencia no se disperse.
/// </summary>
public sealed class AccessTokenReader(ISigningKeyProvider signingKeyProvider, TimeProvider timeProvider) : IAccessTokenReader
{
    private const string InvalidTokenDescription =
        "El access token es invalido, ha caducado o no fue emitido por este mock.";

    private static readonly JsonWebTokenHandler Handler = new();

    public Result<AccessTokenClaims> Read(string accessToken, string issuer, IEnumerable<string>? audiences)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return Result<AccessTokenClaims>.Fail(ProtocolErrors.InvalidToken(InvalidTokenDescription));
        }

        // La clave NO se libera aqui: ISigningKeyProvider la entrega por referencia y es del host
        // (PemSigningKeyProvider la cachea entre peticiones). Hacerla disposable con `using` dejaba al
        // proceso sin clave de firma en la segunda lectura de token, y userinfo o revocation fallaban.
        var signingKey = signingKeyProvider.GetSigningKey();
        var result = Handler.ValidateTokenAsync(
            accessToken,
            BuildParameters(signingKey, issuer, audiences)).GetAwaiter().GetResult();

        if (!result.IsValid)
        {
            return Result<AccessTokenClaims>.Fail(ProtocolErrors.InvalidToken(InvalidTokenDescription));
        }

        return ToClaims(result.Claims);
    }

    /// <summary>
    /// La vida util se comprueba contra el TimeProvider inyectado, no contra el reloj del proceso: si
    /// no, un token emitido con un reloj de pruebas se rechazaria como caducado.
    /// </summary>
    private TokenValidationParameters BuildParameters(
        SigningKey signingKey,
        string issuer,
        IEnumerable<string>? audiences) =>
        new()
        {
            IssuerSigningKeys = [ToValidationKey(signingKey.Key, signingKey.KeyId)],
            ValidAlgorithms = [TokenHeaderValues.SignatureAlgorithm],
            ValidIssuer = issuer,
            ValidAudiences = audiences is null ? null : [.. audiences],
            // Sin lista de audiencias hay que desactivar la comprobacion: la libreria falla si
            // ValidateAudience queda activo sin audiencia esperada configurada.
            ValidateAudience = audiences is not null,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) => IsWithinLifetime(notBefore, expires)
        };

    private bool IsWithinLifetime(DateTime? notBefore, DateTime? expires)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        return (notBefore is null || notBefore <= now) && (expires is null || now < expires);
    }

    private static Microsoft.IdentityModel.Tokens.JsonWebKey ToValidationKey(RSA key, string keyId)
    {
        var parameters = key.ExportParameters(includePrivateParameters: false);

        return new Microsoft.IdentityModel.Tokens.JsonWebKey
        {
            Kty = JsonWebKeyType,
            Kid = keyId,
            N = Base64Url.Encode(parameters.Modulus!),
            E = Base64Url.Encode(parameters.Exponent!)
        };
    }

    private static Result<AccessTokenClaims> ToClaims(IDictionary<string, object> claims)
    {
        var scopes = ReadClaim(claims, ProtocolClaimNames.Scope)
            ?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (scopes is null)
        {
            return Result<AccessTokenClaims>.Fail(ProtocolErrors.InvalidToken(InvalidTokenDescription));
        }

        return Result<AccessTokenClaims>.Ok(new AccessTokenClaims(
            ReadClaim(claims, ProtocolClaimNames.Subject) ?? string.Empty,
            ReadClaim(claims, ProtocolClaimNames.ClientId) ?? string.Empty,
            scopes,
            ReadClaim(claims, ProtocolClaimNames.TokenId) ?? string.Empty,
            ToExpiry(ReadClaim(claims, ProtocolClaimNames.Expiration))));
    }

    private static string? ReadClaim(IDictionary<string, object> claims, string claimName) =>
        claims.TryGetValue(claimName, out var value) ? value?.ToString() : null;

    private static DateTimeOffset ToExpiry(string? exp) =>
        long.TryParse(exp, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : DateTimeOffset.UnixEpoch;

    private const string JsonWebKeyType = "RSA";
}