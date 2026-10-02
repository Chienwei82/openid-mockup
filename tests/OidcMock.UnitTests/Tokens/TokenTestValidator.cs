using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OidcMock.Core.Crypto;
using OidcMock.Core.Discovery;

namespace OidcMock.UnitTests.Tokens;

/// <summary>
/// Valida tokens emitidos por el mock contra el JWKS que publica el propio mock, con el mismo
/// JsonWebTokenHandler. Si el mock se auto-validara, los tests de emision no probarian nada.
/// </summary>
public sealed class TokenTestValidator
{
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly RSA _signingKey;
    private readonly Microsoft.IdentityModel.Tokens.JsonWebKeySet _publishedKeys;

    public TokenTestValidator(RSA signingKey)
    {
        _signingKey = signingKey;
        _publishedKeys = ToValidationKeySet(JsonWebKeySetBuilder.Build(signingKey));
    }

    public string KeyId => SigningKeyId.FromPublicKey(_signingKey);

    /// <summary>
    /// Valida el token y ademas comprueba su vigencia contra <paramref name="timeProvider"/>.
    /// </summary>
    /// <remarks>
    /// El reloj es obligatorio a proposito. Antes era opcional y, al omitirlo, se desactivaba la
    /// comprobacion de <c>exp</c> sin avisar: un test que solo queria mirar el <c>iss</c> pasaba
    /// igual con un token caducado, y el fallo aparecia en produccion. Ahora el reloj se pasa siempre
    /// y omitirlo es un <see cref="ArgumentException"/> en la primera linea.
    /// </remarks>
    public TokenValidationResult Validate(
        string token,
        string expectedIssuer,
        IEnumerable<string> expectedAudiences,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return Handler
            .ValidateTokenAsync(token, BuildParameters(expectedIssuer, expectedAudiences, timeProvider))
            .GetAwaiter()
            .GetResult();
    }

    public static JsonElement ReadPayload(string token) => ReadSegment(token, JwtTokenSegment.Payload);

    public static JsonElement ReadHeader(string token) => ReadSegment(token, JwtTokenSegment.Header);

    public async Task<TokenValidationResult> ValidateAsync(
        string token,
        string expectedIssuer,
        IEnumerable<string> expectedAudiences,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return await Handler.ValidateTokenAsync(token, BuildParameters(expectedIssuer, expectedAudiences, timeProvider));
    }

    public static JsonElement ReadClaim(string token, string claimName)
    {
        var payload = ReadPayload(token);

        return payload.TryGetProperty(claimName, out var claim)
            ? claim
            : throw new KeyNotFoundException($"El token no contiene el claim '{claimName}'.");
    }

    public static bool HasClaim(string token, string claimName) => ReadPayload(token).TryGetProperty(claimName, out _);

    public static IEnumerable<string> ReadClaimNames(string token) =>
        ReadPayload(token).EnumerateObject().Select(property => property.Name);

    private TokenValidationParameters BuildParameters(
        string expectedIssuer,
        IEnumerable<string> expectedAudiences,
        TimeProvider timeProvider) =>
        new()
        {
            IssuerSigningKeys = _publishedKeys.Keys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidIssuer = expectedIssuer,
            ValidAudiences = expectedAudiences,
            ValidateLifetime = true,
            LifetimeValidator = (notBefore, expires, _, _) =>
                IsWithinLifetime(notBefore, expires, timeProvider.GetUtcNow().UtcDateTime)
        };

    private static bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, DateTime utcNow) =>
        (notBefore is null || notBefore <= utcNow) && (expires is null || utcNow < expires);

    private static JsonElement ReadSegment(string token, JwtTokenSegment segment)
    {
        var parts = token.Split('.');

        if (parts.Length != JwtTokenSegmentLimits.ExpectedPartCount)
        {
            throw new FormatException($"El token no es un JWT con {JwtTokenSegmentLimits.ExpectedPartCount} segmentos.");
        }

        var encoded = parts[(int)segment];

        return JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(encoded)).RootElement.Clone();
    }

    private static Microsoft.IdentityModel.Tokens.JsonWebKeySet ToValidationKeySet(
        OidcMock.Core.Discovery.JsonWebKeySet published)
    {
        var validationKeySet = new Microsoft.IdentityModel.Tokens.JsonWebKeySet();

        foreach (var key in published.Keys)
        {
            validationKeySet.Keys.Add(new Microsoft.IdentityModel.Tokens.JsonWebKey
            {
                Kty = key.KeyType,
                Use = key.Use,
                Kid = key.KeyId,
                Alg = key.Algorithm,
                N = key.Modulus,
                E = key.Exponent
            });
        }

        return validationKeySet;
    }
}