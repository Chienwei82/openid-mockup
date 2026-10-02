using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OidcMock.Core.Claims;
using OidcMock.Core.Crypto;
using OidcMock.Core.Tokens;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Grants;

namespace OidcMock.UnitTests.Tokens;

/// <summary>
/// El lector de access tokens es la puerta de userinfo, introspect y revocation, asi que sus tests
/// fijan el reloj: un token emitido en el pasado (o en el futuro) segun el reloj real en vez del
/// inyectado pasaria o se rechazaria por el motivo equivocado.
/// </summary>
public sealed class AccessTokenReaderTests : GrantHandlerTestBase
{
    private const string Audience = "web-app-spa";

    [Fact]
    public void LeeUnTokenValidoEmitidoConElRelojInyectado()
    {
        var reader = Reader();

        var read = reader.Read(IssueToken(), Issuer, [Audience]);

        Assert.True(read.Succeeded, read.Error?.ToString());
        Assert.Equal("user-1", read.Value?.Subject);
        Assert.Equal(ClientId, read.Value?.ClientId);
    }

    /// <summary>
    /// Regresion: la vida util se comprueba contra el TimeProvider inyectado. Con el reloj del proceso
    /// un token emitido con reloj de pruebas se rechazaba como caducado de inmediato.
    /// </summary>
    [Fact]
    public void AceptaUnTokenEmitidoLejosDelRelojDelProceso()
    {
        var read = Reader().Read(IssueToken(), Issuer, [Audience]);

        Assert.True(read.Succeeded, read.Error?.ToString());
    }

    [Fact]
    public void RechazaUnTokenCaducadoEnElRelojInyectado()
    {
        var token = IssueToken();
        Clock.Advance(TimeSpan.FromMinutes(31));

        var read = Reader().Read(token, Issuer, [Audience]);

        Assert.False(read.Succeeded);
        Assert.Equal("invalid_token", read.Error?.Code);
    }

    /// <summary>
    /// JWT bien formado y correctamente firmado, pero emitido en el futuro: su <c>nbf</c> es
    /// posterior al instante de lectura, asi que todavia no es valido. Se construye a mano (y no con
    /// el factory) porque el factory firma siempre con el reloj actual, y justamente de lo que se
    /// trata es de que el lector valide el <c>nbf</c> de cualquier token, no solo de los propios.
    /// </summary>
    [Fact]
    public void RechazaUnTokenQueAunNoEsValido()
    {
        var future = Now.AddMinutes(5).UtcDateTime;

        var token = MintToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = ClientId,
            IssuedAt = future,
            NotBefore = future,
            Expires = future.AddMinutes(30),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [ProtocolClaimNames.Subject] = "user-1",
                [ProtocolClaimNames.ClientId] = ClientId,
                [ProtocolClaimNames.Scope] = "openid"
            },
            SigningCredentials = SigningCredentials()
        });

        var read = Reader().Read(token, Issuer, [Audience]);

        Assert.False(read.Succeeded);
        Assert.Equal("invalid_token", read.Error?.Code);
    }

    [Fact]
    public void RechazaUnTokenDeOtroIssuer()
    {
        var read = Reader().Read(IssueToken(), "https://otro-issuer.example/", [Audience]);

        Assert.False(read.Succeeded);
    }

    /// <summary>
    /// El token viene bien formado pero firmado con una clave que no es la del JWKS. Es el caso que
    /// justifica validar la firma en vez de solo deserializar el token.
    /// </summary>
    [Fact]
    public void RechazaUnTokenFirmadoConOtraClave()
    {
        using var foreignKey = RSA.Create(SigningKeySizes.KeySizeInBits);
        var foreignIssuer = new JsonWebTokenFactory(
            new TestKeyProvider(new SigningKey(foreignKey)),
            ClaimsProjector(),
            Clock);

        var token = foreignIssuer.CreateAccessToken(new AccessTokenRequest(
            Issuer,
            ClientId,
            ["openid"],
            [ClientId],
            "user-1",
            null,
            TimeSpan.FromMinutes(30)));

        var read = Reader().Read(token, Issuer, [Audience]);

        Assert.False(read.Succeeded);
        Assert.Equal("invalid_token", read.Error?.Code);
    }

    [Fact]
    public void RechazaUnaAudienciaQueNoCorresponde()
    {
        var read = Reader().Read(IssueToken(), Issuer, ["otro-cliente"]);

        Assert.False(read.Succeeded);
    }

    /// <summary>
    /// userinfo no conoce la audiencia de antemano (es el client_id del propio token), asi que lee con
    /// la comprobacion desactivada.
    /// </summary>
    [Fact]
    public void SinAudienciasEsperadasNoCompruebaLaAudiencia()
    {
        var read = Reader().Read(IssueToken(), Issuer, audiences: null);

        Assert.True(read.Succeeded, read.Error?.ToString());
    }

    [Fact]
    public void RechazaUnTokenQueNoEsUnJwt()
    {
        Assert.False(Reader().Read("no-es-un-jwt", Issuer, [Audience]).Succeeded);
    }

    [Fact]
    public void LeeLosScopesYElTokenIdDelToken()
    {
        var read = Reader().Read(IssueToken(scopes: ["openid", "email", "profile"]), Issuer, [Audience]);

        Assert.Equal(["openid", "email", "profile"], read.Value?.Scopes);
        Assert.False(string.IsNullOrWhiteSpace(read.Value?.TokenId));
    }

    /// <summary>
    /// Regresion: el lector hacia <c>using</c> sobre la clave que le entrega el proveedor, que es del
    /// host y esta cacheada entre peticiones. En el proceso real eso dejaba al mock sin clave de firma
    /// para la **segunda** lectura: /userinfo, /introspect y /revocation respondian 500 desde el
    /// segundo token en adelante. Con el mismo lector se releen varios tokens seguidos.
    /// </summary>
    [Fact]
    public void LeeVariosTokensSeguidosConLaMismaClaveDelProveedor()
    {
        var reader = Reader();

        for (var intento = 0; intento < 3; intento++)
        {
            var read = reader.Read(IssueToken(), Issuer, [Audience]);

            Assert.True(read.Succeeded, $"El intento {intento} fallo: {read.Error}");
        }
    }

    private AccessTokenReader Reader() => new(SigningKeyProvider, Clock);

    /// <summary>Mina un JWT con la clave del fixture, para los casos que el factory no puede expresar.</summary>
    private static string MintToken(SecurityTokenDescriptor descriptor) =>
        new JsonWebTokenHandler().CreateToken(descriptor);

    /// <summary>
    /// Credenciales con la clave del fixture. La clave no se libera aqui: su ciclo de vida es el del
    /// fixture, que ya la dispone para las pruebas unitarias.
    /// </summary>
    private SigningCredentials SigningCredentials() =>
        new(new RsaSecurityKey(SigningKey), SecurityAlgorithms.RsaSha256);

    private static Core.Claims.ScopesClaimsProjector ClaimsProjector() => new(
        ScopeStoreFixture.Create());

    /// <summary>Entrega una clave concreta al factory, para firmar con una que no es la del JWKS.</summary>
    private sealed class TestKeyProvider(Core.Crypto.SigningKey key) : Core.Crypto.ISigningKeyProvider
    {
        public Core.Crypto.SigningKey GetSigningKey() => key;
    }

    private string IssueToken(IReadOnlyList<string>? scopes = null) =>
        Tokens.CreateAccessToken(new AccessTokenRequest(
            Issuer,
            ClientId,
            scopes ?? ["openid"],
            [ClientId],
            "user-1",
            null,
                        TimeSpan.FromMinutes(30)));

    private const string ClientId = "web-app-spa";
}