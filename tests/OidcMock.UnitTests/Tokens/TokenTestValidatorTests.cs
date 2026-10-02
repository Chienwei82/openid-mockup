using System.Security.Cryptography;
using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Claims;
using OidcMock.Core.Crypto;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;
using OidcMock.UnitTests.Fixtures;

namespace OidcMock.UnitTests.Tokens;

/// <summary>
/// El validador que usan las pruebas de emision es el que decide si una comprobacion mira la vigencia
/// del token. Si el reloj es opcional, omitirlo desactiva <c>exp</c> en silencio y el test sigue
/// pasando con un token caducado hace años.
/// </summary>
public sealed class TokenTestValidatorTests
{
    private const string Issuer = "https://localhost:5001/personafisica/";
    private const string ClientId = "web-app-spa";

    /// <summary>
    /// Reloj fijo. Firmar y validar contra el mismo instante es lo que hace comparables los dos
    /// tokens: uno que caduca "en una hora" y otro que caducaba "hace una hora" salen de aqui.
    /// </summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Misma clave para firmar y para validar: si no, fallaria la firma y no la vigencia.</summary>
    private readonly RSA _signingKey = RSA.Create(SigningKeySizes.KeySizeInBits);

    private readonly TokenTestValidator _validator;

    public TokenTestValidatorTests() => _validator = new TokenTestValidator(_signingKey);

    [Fact]
    public void SinRelojNoSePuedeValidarPorqueNoMirariaLaVigencia()
    {
        var token = CreateToken(_clock.GetUtcNow().AddHours(-2), TimeSpan.FromHours(1));

        var exception = Assert.Throws<ArgumentNullException>(() =>
            _validator.Validate(token, Issuer, [ClientId], null!));

        Assert.Equal("timeProvider", exception.ParamName);
    }

    [Fact]
    public void ConRelojElTokenCaducadoNoValida()
    {
        var token = CreateToken(_clock.GetUtcNow().AddHours(-2), TimeSpan.FromHours(1));

        var result = _validator.Validate(token, Issuer, [ClientId], _clock);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ConRelojElTokenVigenteValida()
    {
        var token = CreateToken(_clock.GetUtcNow(), TimeSpan.FromMinutes(30));

        var result = _validator.Validate(token, Issuer, [ClientId], _clock);

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    private string CreateToken(DateTimeOffset issuedAt, TimeSpan lifetime) =>
        new JsonWebTokenFactory(
                new SigningKeyOfMine(_signingKey),
                new ScopesClaimsProjector(ScopeStoreFixture.Create()),
                new FakeTimeProvider(issuedAt))
            .CreateIdToken(new IdTokenRequest(
                Issuer,
                ClientId,
                ["openid"],
                SampleUser,
                issuedAt,
                lifetime,
                Nonce: null,
                AccessToken: null,
                AuthorizationCode: null));

    private static readonly User SampleUser = new("user-1", "jperez", "clave", new Dictionary<string, System.Text.Json.JsonElement>());

    private sealed class SigningKeyOfMine(RSA key) : ISigningKeyProvider
    {
        public SigningKey GetSigningKey() => new(key);
    }
}