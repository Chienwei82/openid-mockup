using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OidcMock.Core.Claims;
using OidcMock.Core.Crypto;
using OidcMock.Core.Discovery;
using OidcMock.Core.Errors;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Validacion de firma, issuer y vigencia de los tokens del mock, compartida por el lector de access
/// tokens y el de id tokens. Es lo que hace que un token firmado por otro se rechace en vez de
/// deserializarse. La vida util se comprueba contra el TimeProvider inyectado, no contra el reloj del
/// proceso: si no, un token emitido con un reloj de pruebas se rechazaria como caducado.
/// </summary>
public sealed class SignedTokenValidator(ISigningKeyProvider signingKeyProvider, TimeProvider timeProvider)
{
    private static readonly JsonWebTokenHandler Handler = new();

    /// <summary>
    /// Claims del token si es valido, o el error de protocolo con el que responder. <c>audiences</c>
    /// null o vacio desactiva la comprobacion de audiencia: la libreria falla si <c>ValidateAudience</c>
    /// queda activo sin audiencia esperada configurada.
    /// </summary>
    public Result<IDictionary<string, object>> Validate(
        string token,
        string issuer,
        IEnumerable<string>? audiences,
        ProtocolError failure)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Result<IDictionary<string, object>>.Fail(failure);
        }

        // La clave NO se libera aqui: ISigningKeyProvider la entrega por referencia y es del host
        // (PemSigningKeyProvider la cachea entre peticiones). Hacerla disposable con `using` dejaba al
        // proceso sin clave de firma en la segunda lectura de token, y userinfo o revocation fallaban.
        var signingKey = signingKeyProvider.GetSigningKey();
        var result = Handler.ValidateTokenAsync(
            token,
            BuildParameters(signingKey, issuer, audiences)).GetAwaiter().GetResult();

        return result.IsValid
            ? Result<IDictionary<string, object>>.Ok(result.Claims)
            : Result<IDictionary<string, object>>.Fail(failure);
    }

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

    private const string JsonWebKeyType = "RSA";
}