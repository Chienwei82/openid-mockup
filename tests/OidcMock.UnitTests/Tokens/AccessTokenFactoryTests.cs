using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Claims;
using OidcMock.Core.Crypto;
using OidcMock.Core.Scopes;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;

namespace OidcMock.UnitTests.Tokens;

public sealed class AccessTokenFactoryTests
{
    private const string Issuer = "https://localhost:5001/personafisica/";
    private const string ClientId = "backend-service";
    private const int AccessTokenLifetimeInMinutes = 60;
    private const string LifetimeValidationFailure = "IDX10230";

    private static readonly DateTimeOffset IssuedAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly RSA _signingKey = RSA.Create(SigningKeySizes.KeySizeInBits);
    private readonly FakeTimeProvider _clock = new(IssuedAt);
    private readonly TokenTestValidator _validator;
    private readonly JsonWebTokenFactory _factory;

    public AccessTokenFactoryTests()
    {
        _validator = new TokenTestValidator(_signingKey);
        _factory = new JsonWebTokenFactory(
            new FixedSigningKeyProvider(new SigningKey(_signingKey)),
            new ScopesClaimsProjector(Scopes()),
            _clock);
    }

    [Fact]
    public void EsValidableContraElJwksYDeclaraElTipoAtJwt()
    {
        var token = CreateAccessToken();

        var result = _validator.Validate(token, Issuer, [ClientId], _clock);
        var header = TokenTestValidator.ReadHeader(token);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("at+jwt", header.GetProperty("typ").GetString());
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal(_validator.KeyId, header.GetProperty("kid").GetString());
    }

    [Fact]
    public void EmiteLosClaimsDeProtocoloDelAccessToken()
    {
        var token = CreateAccessToken();

        Assert.Equal(Issuer, TokenTestValidator.ReadClaim(token, "iss").GetString());
        Assert.Equal("user-1", TokenTestValidator.ReadClaim(token, "sub").GetString());
        Assert.Equal(ClientId, TokenTestValidator.ReadClaim(token, "client_id").GetString());
        Assert.Equal("openid custom.profile roles", TokenTestValidator.ReadClaim(token, "scope").GetString());
        Assert.Equal(IssuedAt.ToUnixTimeSeconds(), TokenTestValidator.ReadClaim(token, "iat").GetInt64());
        Assert.Equal(IssuedAt.AddMinutes(AccessTokenLifetimeInMinutes).ToUnixTimeSeconds(), TokenTestValidator.ReadClaim(token, "exp").GetInt64());
    }

    [Fact]
    public void EmiteUnIdentificadorUnicoPorToken()
    {
        var identifiers = Enumerable.Range(0, 10)
            .Select(_ => TokenTestValidator.ReadClaim(CreateAccessToken(), "jti").GetString())
            .ToList();

        Assert.Equal(identifiers.Count, identifiers.Distinct(StringComparer.Ordinal).Count());
        Assert.All(identifiers, identifier => Assert.False(string.IsNullOrWhiteSpace(identifier)));
    }

    [Fact]
    public void LaAudienciaEsLaDefinidaPorElClienteYNoElSubject()
    {
        var token = _factory.CreateAccessToken(Request(["https://api.bccr.fi.cr/centralenlinea"]));

        var result = _validator.Validate(token, Issuer, ["https://api.bccr.fi.cr/centralenlinea"], _clock);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("https://api.bccr.fi.cr/centralenlinea", TokenTestValidator.ReadClaim(token, "aud").GetString());
    }

    [Fact]
    public void AdmiteVariasAudienciasSimultaneas()
    {
        string[] audiences = ["https://api.bccr.fi.cr/centralenlinea", "https://api.bccr.fi.cr/bolsadeempleo"];

        var token = _factory.CreateAccessToken(Request(audiences));

        var result = _validator.Validate(token, Issuer, audiences, _clock);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(audiences.Length, TokenTestValidator.ReadClaim(token, "aud").GetArrayLength());
    }

    [Fact]
    public void ElAccessTokenCaducaCuandoElRelojAvanzaSuVigencia()
    {
        var token = CreateAccessToken();

        _clock.Advance(TimeSpan.FromMinutes(AccessTokenLifetimeInMinutes));

        var result = _validator.Validate(token, Issuer, [ClientId], _clock);

        Assert.False(result.IsValid);
        Assert.Contains(LifetimeValidationFailure, result.Exception?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProyectaLosClaimsDelUsuarioPermitidosPorLosScopes()
    {
        var token = CreateAccessToken();

        var claims = TokenTestValidator.ReadClaimNames(token).ToList();

        Assert.Contains("full_name", claims);
        Assert.Contains("role", claims);
        Assert.DoesNotContain("email", claims);
    }

    [Fact]
    public void SinUsuarioElAccessTokenNoIncluyeClaimsDeUsuario()
    {
        var token = _factory.CreateAccessToken(new AccessTokenRequest(
            Issuer,
            ClientId,
            ["openid", "custom.profile"],
            [ClientId],
            "user-1",
            null,
                        TimeSpan.FromMinutes(AccessTokenLifetimeInMinutes)));

        Assert.DoesNotContain("full_name", TokenTestValidator.ReadClaimNames(token));
    }

    [Fact]
    public void ElAccessTokenSeRechazaCuandoLoFirmaUnaClaveDistinta()
    {
        using var impostorKey = RSA.Create(SigningKeySizes.KeySizeInBits);
        var impostorValidator = new TokenTestValidator(impostorKey);
        var result = impostorValidator.Validate(CreateAccessToken(), Issuer, [ClientId], _clock);

        Assert.False(result.IsValid);
    }

    private string CreateAccessToken() => _factory.CreateAccessToken(Request([ClientId]));

    private static AccessTokenRequest Request(IReadOnlyList<string> audiences) => new(
        Issuer,
        ClientId,
        ["openid", "custom.profile", "roles"],
        audiences,
        "user-1",
        SampleUser(),
                TimeSpan.FromMinutes(AccessTokenLifetimeInMinutes));

    private static InMemoryScopeStore Scopes() =>
        new InMemoryScopeStore(
        [
            new ScopeDefinition("openid", ["sub"]),
            new ScopeDefinition("email", ["email", "email_verified"]),
            new ScopeDefinition("custom.profile", ["full_name"]),
            new ScopeDefinition("roles", ["role"]),
            new ScopeDefinition("offline_access", [])
        ]);

    private static readonly string[] SampleRoles = ["administrador"];

    private static User SampleUser() => new(
        "user-1",
        "jperez",
        "clave",
        new Dictionary<string, JsonElement>
        {
            ["full_name"] = JsonSerializer.SerializeToElement("JUAN PEREZ LOPEZ"),
            ["email"] = JsonSerializer.SerializeToElement("jperez@example.cr"),
            ["email_verified"] = JsonSerializer.SerializeToElement(true),
            ["role"] = JsonSerializer.SerializeToElement<string[]>(SampleRoles)
        });

    [Fact]
    public void ElAccessTokenTomaIatNbfYExpDelMismoInstanteDelReloj()
    {
        var factory = new JsonWebTokenFactory(
            new FixedSigningKeyProvider(new SigningKey(_signingKey)),
            new ScopesClaimsProjector(Scopes()),
            new AdvancingTimeProvider(IssuedAt));

        var token = factory.CreateAccessToken(new AccessTokenRequest(
            Issuer,
            ClientId,
            ["openid"],
            [ClientId],
            "user-1",
            SampleUser(),
                        TimeSpan.FromMinutes(AccessTokenLifetimeInMinutes)));

        var issuedAt = TokenTestValidator.ReadClaim(token, "iat").GetInt64();
        var notBefore = TokenTestValidator.ReadClaim(token, "nbf").GetInt64();
        var expires = TokenTestValidator.ReadClaim(token, "exp").GetInt64();

        Assert.Equal(issuedAt, notBefore);
        Assert.Equal(issuedAt, expires - (AccessTokenLifetimeInMinutes * 60));
    }

    private sealed class AdvancingTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow()
        {
            var current = _now;

            _now = _now.AddSeconds(1);

            return current;
        }
    }

    private sealed class FixedSigningKeyProvider(SigningKey key) : ISigningKeyProvider
    {
        public SigningKey GetSigningKey() => key;
    }
}

