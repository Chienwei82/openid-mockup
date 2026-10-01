using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
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

    private static readonly DateTimeOffset IssuedAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly RSA _signingKey = RSA.Create(SigningKeySizes.KeySizeInBits);
    private readonly FakeTimeProvider _clock = new(IssuedAt);
    private readonly TokenTestValidator _validator;
    private readonly ITokenFactory _factory;

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

        var result = _validator.Validate(token, Issuer, [ClientId]);
        var header = _validator.ReadHeader(token);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("at+jwt", header.GetProperty("typ").GetString());
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal(_validator.KeyId, header.GetProperty("kid").GetString());
    }

    [Fact]
    public void EmiteLosClaimsDeProtocoloDelAccessToken()
    {
        var token = CreateAccessToken();

        Assert.Equal(Issuer, _validator.ReadClaim(token, "iss").GetString());
        Assert.Equal("user-1", _validator.ReadClaim(token, "sub").GetString());
        Assert.Equal(ClientId, _validator.ReadClaim(token, "client_id").GetString());
        Assert.Equal("openid custom.profile roles", _validator.ReadClaim(token, "scope").GetString());
        Assert.Equal(IssuedAt.ToUnixTimeSeconds(), _validator.ReadClaim(token, "iat").GetInt64());
        Assert.Equal(IssuedAt.AddMinutes(AccessTokenLifetimeInMinutes).ToUnixTimeSeconds(), _validator.ReadClaim(token, "exp").GetInt64());
    }

    [Fact]
    public void EmiteUnIdentificadorUnicoPorToken()
    {
        var identifiers = Enumerable.Range(0, 10)
            .Select(_ => _validator.ReadClaim(CreateAccessToken(), "jti").GetString())
            .ToList();

        Assert.Equal(identifiers.Count, identifiers.Distinct(StringComparer.Ordinal).Count());
        Assert.All(identifiers, identifier => Assert.False(string.IsNullOrWhiteSpace(identifier)));
    }

    [Fact]
    public void LaAudienciaEsLaDefinidaPorElClienteYNoElSubject()
    {
        var token = _factory.CreateAccessToken(Request(["https://api.bccr.fi.cr/centralenlinea"]));

        var result = _validator.Validate(token, Issuer, ["https://api.bccr.fi.cr/centralenlinea"]);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("https://api.bccr.fi.cr/centralenlinea", _validator.ReadClaim(token, "aud").GetString());
    }

    [Fact]
    public void AdmiteVariasAudienciasSimultaneas()
    {
        string[] audiences = ["https://api.bccr.fi.cr/centralenlinea", "https://api.bccr.fi.cr/bolsadeempleo"];

        var token = _factory.CreateAccessToken(Request(audiences));

        var result = _validator.Validate(token, Issuer, audiences);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(audiences.Length, _validator.ReadClaim(token, "aud").GetArrayLength());
    }

    [Fact]
    public void ElAccessTokenCaducaCuandoElRelojAvanzaSuVigencia()
    {
        var token = CreateAccessToken();

        _clock.Advance(TimeSpan.FromMinutes(AccessTokenLifetimeInMinutes));

        var result = _validator.Validate(token, Issuer, [ClientId], _clock);

        Assert.False(result.IsValid);
        Assert.Contains("expired", result.Exception?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProyectaLosClaimsDelUsuarioPermitidosPorLosScopes()
    {
        var token = CreateAccessToken();

        var claims = _validator.ReadClaimNames(token).ToList();

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
            IssuedAt,
            TimeSpan.FromMinutes(AccessTokenLifetimeInMinutes)));

        Assert.DoesNotContain("full_name", _validator.ReadClaimNames(token));
    }

    [Fact]
    public void ElAccessTokenSeRechazaCuandoLoFirmaUnaClaveDistinta()
    {
        using var impostorKey = RSA.Create(SigningKeySizes.KeySizeInBits);
        var impostorValidator = new TokenTestValidator(impostorKey);
        var impostorFactory = new JsonWebTokenFactory(
            new FixedSigningKeyProvider(new SigningKey(impostorKey)),
            new ScopesClaimsProjector(Scopes()),
            _clock);

        var result = impostorValidator.Validate(impostorFactory.CreateAccessToken(Request([ClientId])), Issuer, [ClientId]);

        Assert.False(result.IsValid);
    }

    private string CreateAccessToken() => _factory.CreateAccessToken(Request([ClientId]));

    private AccessTokenRequest Request(IReadOnlyList<string> audiences) => new(
        Issuer,
        ClientId,
        ["openid", "custom.profile", "roles"],
        audiences,
        "user-1",
        SampleUser(),
        IssuedAt,
        TimeSpan.FromMinutes(AccessTokenLifetimeInMinutes));

    private static IScopeStore Scopes() =>
        new InMemoryScopeStore(
        [
            new ScopeDefinition("openid", ["sub"]),
            new ScopeDefinition("email", ["email", "email_verified"]),
            new ScopeDefinition("custom.profile", ["full_name"]),
            new ScopeDefinition("roles", ["role"]),
            new ScopeDefinition("offline_access", [])
        ]);

    private static User SampleUser() => new(
        "user-1",
        "jperez",
        "clave",
        new Dictionary<string, JsonElement>
        {
            ["full_name"] = JsonSerializer.SerializeToElement("JUAN PEREZ LOPEZ"),
            ["email"] = JsonSerializer.SerializeToElement("jperez@example.cr"),
            ["email_verified"] = JsonSerializer.SerializeToElement(true),
            ["role"] = JsonSerializer.SerializeToElement(new[] { "administrador" })
        });

    private sealed class FixedSigningKeyProvider(SigningKey key) : ISigningKeyProvider
    {
        public SigningKey GetSigningKey() => key;
    }
}

