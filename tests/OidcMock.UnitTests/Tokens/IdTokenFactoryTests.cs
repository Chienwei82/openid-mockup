using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Claims;
using OidcMock.Core.Crypto;
using OidcMock.Core.Scopes;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;

namespace OidcMock.UnitTests.Tokens;

public sealed class IdTokenFactoryTests
{
    private const string Issuer = "https://localhost:5001/personafisica/";
    private const string ClientId = "web-app-spa";
    private const int IdentityTokenLifetimeInMinutes = 30;
    private const string LifetimeValidationFailure = "IDX10230";

    private static readonly DateTimeOffset AuthenticatedAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly RSA _signingKey = RSA.Create(SigningKeySizes.KeySizeInBits);
    private readonly FakeTimeProvider _clock = new(AuthenticatedAt);
    private readonly TokenTestValidator _validator;
    private readonly JsonWebTokenFactory _factory;

    public IdTokenFactoryTests()
    {
        _validator = new TokenTestValidator(_signingKey);
        _factory = new JsonWebTokenFactory(
            new FixedSigningKeyProvider(new SigningKey(_signingKey)),
            new ScopesClaimsProjector(Scopes()),
            _clock);
    }

    [Fact]
    public void FirmaConLaClaveDelMockYEsValidableContraElJwks()
    {
        var token = CreateIdToken();

        var result = _validator.Validate(token, Issuer, [ClientId]);

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public void ElHeaderDeclaraRs256ElKidDelMockYTipoJwt()
    {
        var header = TokenTestValidator.ReadHeader(CreateIdToken());

        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal(_validator.KeyId, header.GetProperty("kid").GetString());
        Assert.Equal("JWT", header.GetProperty("typ").GetString());
    }

    [Fact]
    public void EmiteLosClaimsDeProtocoloDelIdToken()
    {
        var token = CreateIdToken();

        var result = _validator.Validate(token, Issuer, [ClientId]);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(Issuer, TokenTestValidator.ReadClaim(token, "iss").GetString());
        Assert.Equal("user-1", TokenTestValidator.ReadClaim(token, "sub").GetString());
        Assert.Equal(ClientId, TokenTestValidator.ReadClaim(token, "aud").GetString());
        Assert.Equal(AuthenticatedAt.ToUnixTimeSeconds(), TokenTestValidator.ReadClaim(token, "auth_time").GetInt64());
        Assert.Equal(AuthenticatedAt.ToUnixTimeSeconds(), TokenTestValidator.ReadClaim(token, "iat").GetInt64());
    }

    [Fact]
    public void ExpiraSegunLaVigenciaDelClienteTomadaDelRelojInyectado()
    {
        var token = CreateIdToken();

        var expires = TokenTestValidator.ReadClaim(token, "exp").GetInt64();

        Assert.Equal(AuthenticatedAt.AddMinutes(IdentityTokenLifetimeInMinutes).ToUnixTimeSeconds(), expires);
    }

    [Fact]
    public void ElTokenCaducaCuandoElRelojAvanzaSuVigencia()
    {
        var token = CreateIdToken();

        _clock.Advance(TimeSpan.FromMinutes(IdentityTokenLifetimeInMinutes));

        var result = _validator.Validate(token, Issuer, [ClientId], _clock);

        Assert.False(result.IsValid);
        Assert.Contains(LifetimeValidationFailure, result.Exception?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ElTokenSigueVigenteAntesDeCumplirSuVigencia()
    {
        var token = CreateIdToken();

        _clock.Advance(TimeSpan.FromMinutes(IdentityTokenLifetimeInMinutes - 1));

        var result = _validator.Validate(token, Issuer, [ClientId], _clock);

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public void PropagaElNonceCuandoLaPeticionLoTrae()
    {
        var token = CreateIdToken(nonce: "n-0S6_WzA2Mj");

        Assert.Equal("n-0S6_WzA2Mj", TokenTestValidator.ReadClaim(token, "nonce").GetString());
    }

    [Fact]
    public void OmiteElNonceCuandoLaPeticionNoLoTrae()
    {
        var token = CreateIdToken();

        Assert.False(TokenTestValidator.HasClaim(token, "nonce"));
    }

    [Fact]
    public void CalculaAtHashConLaMitadIzquierdaDelSha256DelAccessToken()
    {
        const string accessToken = "header.payload.signature";

        var token = CreateIdToken(accessToken: accessToken);

        Assert.Equal(ExpectedLeftHalfHash(accessToken), TokenTestValidator.ReadClaim(token, "at_hash").GetString());
    }

    [Fact]
    public void CalculaCHashConLaMitadIzquierdaDelSha256DelCodigo()
    {
        const string authorizationCode = "SplxlOBeZQQYbYS6WxSbIA";

        var token = CreateIdToken(authorizationCode: authorizationCode);

        Assert.Equal(ExpectedLeftHalfHash(authorizationCode), TokenTestValidator.ReadClaim(token, "c_hash").GetString());
    }

    [Fact]
    public void OmiteAtHashYCHashCuandoNoHayTokensQueEnlazar()
    {
        var token = CreateIdToken();

        Assert.False(TokenTestValidator.HasClaim(token, "at_hash"));
        Assert.False(TokenTestValidator.HasClaim(token, "c_hash"));
    }

    [Fact]
    public void SoloExponeLosClaimsDeUsuarioAutorizadosPorLosScopesSolicitados()
    {
        var token = CreateIdToken(scopes: ["openid", "email"]);

        var claims = TokenTestValidator.ReadClaimNames(token).ToList();

        Assert.Contains("email", claims);
        Assert.Contains("email_verified", claims);
        Assert.DoesNotContain("name", claims);
        Assert.DoesNotContain("full_name", claims);
    }

    [Fact]
    public void ConsolidaLosClaimsDeVariosScopesSolicitados()
    {
        var token = CreateIdToken(scopes: ["openid", "email", "custom.profile", "roles"]);

        var claims = TokenTestValidator.ReadClaimNames(token).ToList();

        Assert.Contains("email", claims);
        Assert.Contains("full_name", claims);
        Assert.Contains("role", claims);
    }

    [Fact]
    public void UnScopeSinClaimsNoFiltraDatosDelUsuario()
    {
        var token = CreateIdToken(scopes: ["openid", "offline_access"]);

        var userClaimNames = SampleUser().Claims.Keys.Except(["sub"], StringComparer.Ordinal);

        Assert.NotEmpty(userClaimNames);
        Assert.All(userClaimNames, claimName => Assert.DoesNotContain(claimName, TokenTestValidator.ReadClaimNames(token)));
    }

    [Fact]
    public void ElTokenSeRechazaConAudienciaDistintaALaDelCliente()
    {
        var token = CreateIdToken();

        var result = _validator.Validate(token, Issuer, ["otro-cliente"]);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ElTokenSeRechazaConEmisorDistintoAlDelMock()
    {
        var token = CreateIdToken();

        var result = _validator.Validate(token, "https://otro-issuer.example/", [ClientId]);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ElTokenSeRechazaCuandoLoFirmaUnaClaveDistinta()
    {
        using var impostorKey = RSA.Create(SigningKeySizes.KeySizeInBits);
        var impostorValidator = new TokenTestValidator(impostorKey);

        var result = impostorValidator.Validate(CreateIdToken(), Issuer, [ClientId]);

        Assert.False(result.IsValid);
    }

    private string CreateIdToken(
        IReadOnlyList<string>? scopes = null,
        string? nonce = null,
        string? accessToken = null,
        string? authorizationCode = null) =>
        _factory.CreateIdToken(new IdTokenRequest(
            Issuer,
            ClientId,
            scopes ?? ["openid", "email", "custom.profile"],
            SampleUser(),
            AuthenticatedAt,
            TimeSpan.FromMinutes(IdentityTokenLifetimeInMinutes),
            nonce,
            accessToken,
            authorizationCode));

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
            ["name"] = JsonSerializer.SerializeToElement("Juan Perez"),
            ["email"] = JsonSerializer.SerializeToElement("jperez@example.cr"),
            ["email_verified"] = JsonSerializer.SerializeToElement(true),
            ["full_name"] = JsonSerializer.SerializeToElement("JUAN PEREZ LOPEZ"),
            ["role"] = JsonSerializer.SerializeToElement<string[]>(SampleRoles)
        });

    private static string ExpectedLeftHalfHash(string token)
    {
        var digest = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(token));

        return Base64Url.Encode(digest.AsSpan(0, digest.Length / 2));
    }

    [Fact]
    public void IatNbfYExpSeTomanDelMismoInstanteDelReloj()
    {
        var advancingClock = new AdvancingTimeProvider(AuthenticatedAt);
        var factory = new JsonWebTokenFactory(
            new FixedSigningKeyProvider(new SigningKey(_signingKey)),
            new ScopesClaimsProjector(Scopes()),
            advancingClock);

        var token = factory.CreateIdToken(new IdTokenRequest(
            Issuer,
            ClientId,
            ["openid"],
            SampleUser(),
            AuthenticatedAt,
            TimeSpan.FromMinutes(IdentityTokenLifetimeInMinutes)));

        var issuedAt = TokenTestValidator.ReadClaim(token, "iat").GetInt64();
        var notBefore = TokenTestValidator.ReadClaim(token, "nbf").GetInt64();
        var expires = TokenTestValidator.ReadClaim(token, "exp").GetInt64();

        Assert.Equal(issuedAt, notBefore);
        Assert.Equal(issuedAt, expires - (IdentityTokenLifetimeInMinutes * 60));
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
