using OidcMock.Core.Clients;
using OidcMock.Core.Grants;
using OidcMock.Core.Authorization;
using OidcMock.Core.PendingRequests;
using OidcMock.Core.Tokens;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Tokens;

namespace OidcMock.UnitTests.Grants;

/// <summary>
/// <c>TokenResponseFactory</c> decide que tokens sale de una peticion de token valida, y esa decision la
/// toman todos los grants a la vez: authorization_code, refresh_token, password y los dos de sondeo.
/// Antes no tenia pruebas propias, asi que una regla equivocada aqui afectaria a todos a la vez y solo se
/// veria en el flujo que fallara.
///
/// La regla que importa es de OpenID Connect Core 3.1.3.6 y de RFC 6749: <c>id_token</c> solo con
/// <c>openid</c>, <c>refresh_token</c> solo con <c>offline_access</c>. Devolver un id_token a un canje
/// que no lo pidio es inventar una identidad; devolver un refresh token a un cliente sin
/// <c>offline_access</c> es darle una credencial de larga vida que no pidio.
/// </summary>
public sealed class TokenResponseFactoryTests : GrantHandlerTestBase
{
    private readonly InMemoryPendingAuthorizationStore _pending;

    public TokenResponseFactoryTests() => _pending = new InMemoryPendingAuthorizationStore(Clock);

    [Fact]
    public void EmiteAccessTokenIdTokenYRefreshTokenConOpenidYOfflineAccess()
    {
        var response = Issue(["openid", "offline_access"], includeIdToken: true, includeRefreshToken: true);

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.False(string.IsNullOrWhiteSpace(response.Value!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.Value.IdToken));
        Assert.False(string.IsNullOrWhiteSpace(response.Value.RefreshToken));
    }

    /// <summary>
    /// El grant decide si emite id_token, no la factory: aqui solo se comprueba que la peticion lo pide y
    /// la factory lo respeta, porque ambos decides el mismo rasgo y no pueden contradecirse.
    /// </summary>
    [Fact]
    public void SinPedirIdTokenNoSeEmiteAunqueHayaOpenid()
    {
        var response = Issue(["openid"], includeIdToken: false);

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.Null(response.Value!.IdToken);
    }

    [Fact]
    public void SinPedirRefreshTokenNoSeEmiteAunqueHayaOfflineAccess()
    {
        var response = Issue(["openid", "offline_access"], includeRefreshToken: false);

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.Null(response.Value!.RefreshToken);
    }

    /// <summary>
    /// El <c>expires_in</c> es la vida del access token del cliente. Si fuera una constante, el cliente
    /// refrescaria antes o tarde de lo que el token aguanta.
    /// </summary>
    [Fact]
    public void ElExpiresInEsLaVidaDelAccessTokenDelCliente()
    {
        var client = Clients.Find(ClientStoreFixture.SpaClientId)!;

        var response = Issue(["openid"], forClient: client);

        Assert.Equal((long)client.TokenLifetimes.AccessToken.TotalSeconds, response.Value!.ExpiresIn);
    }

    [Fact]
    public void ElScopeDevueltoEsElConcedido()
    {
        var response = Issue(["openid", "email", "roles"]);

        Assert.Equal("openid email roles", response.Value!.Scope);
    }

    [Fact]
    public void ElAccessTokenSeEmiteConElSubjectDelUsuario()
    {
        var response = Issue(["openid"]);

        var token = Validator.Validate(
            response.Value!.AccessToken,
            Issuer,
            [ClientStoreFixture.SpaClientId],
            Clock);

        Assert.True(token.IsValid, token.Exception?.Message);
        Assert.Equal("user-1", TokenTestValidator.ReadClaim(response.Value.AccessToken, "sub").GetString());
    }

    [Fact]
    public void ElIdTokenLlevaElNonceCuandoSePasaUno()
    {
        var response = Issue(["openid"], nonce: "nonce-de-prueba");

        Assert.Equal(
            "nonce-de-prueba",
            TokenTestValidator.ReadClaim(response.Value!.IdToken!, "nonce").GetString());
    }

    /// <summary>
    /// Store propio: <see cref="IRefreshTokenStore.Redeem"/> es la unica forma de leer un refresh token y
    /// lo consume al hacerlo, asi que investigar el emitido sobre el store compartido dejaria a los
    /// demas tests sin su token.
    /// </summary>
    [Fact]
    public void ElRefreshTokenRegistraLosScopesConcedidosYLaFamilia()
    {
        var store = new InMemoryRefreshTokenStore(Clock);

        var response = Issue(["openid", "offline_access"], refreshTokenFamilyId: "familia-1", into: store);

        var stored = store.Redeem(response.Value!.RefreshToken!);

        Assert.True(stored.Succeeded, stored.Error?.ToString());
        Assert.Equal("familia-1", stored.Value!.FamilyId);
        Assert.Equal(["openid", "offline_access"], stored.Value.Scopes);
    }

    /// <summary>
    /// Sin familia previa, el token abre la suya: sin ella, la deteccion de reutilizacion no tendria contra
    /// que revocar cuando uno de sus hermanos aparezca otra vez.
    /// </summary>
    [Fact]
    public void SinFamiliaPredefinidaElRefreshTokenAbreLaSuya()
    {
        var store = new InMemoryRefreshTokenStore(Clock);

        var response = Issue(["openid", "offline_access"], into: store);

        var stored = store.Redeem(response.Value!.RefreshToken!);

        Assert.True(stored.Succeeded, stored.Error?.ToString());
        Assert.False(string.IsNullOrWhiteSpace(stored.Value!.FamilyId));
    }

    /// <summary>
    /// El <c>id_token</c> solo existe con el scope <c>openid</c> (OpenID Connect Core 3.1.3.6). Un canje
    /// con <c>scope=email</c> llega aqui sin ese scope, y devolver una identidad seria inventarla.
    ///
    /// Se comprueba sobre los dos grants que emitian id_token sin mirar los scopes. Authorization_code y
    /// refresh_token ya aplicaban la regla.
    /// </summary>
    [Fact]
    public async Task PasswordGrantSinOpenidNoEmiteIdToken()
    {
        var granted = await new PasswordGrantHandler(UserStore, RefreshTokens, Tokens, Clock)
            .HandleAsync(PasswordRequest(scopes: ["email"]));

        Assert.True(granted.Succeeded, granted.Error?.ToString());
        Assert.Null(granted.Value!.IdToken);
    }

    [Fact]
    public async Task ElSondeoSinOpenidNoEmiteIdToken()
    {
        var issued = _pending.Issue(new PendingAuthorizationRequest(
            "dc-sin-openid",
            ClientStoreFixture.SpaClientId,
            ["email"],
            new ValidatedAuthorizationRequest(
                Clients.Find(ClientStoreFixture.SpaClientId)!,
                RedirectUri,
                ["email"],
                "code",
                "query",
                Nonce: null,
                State: null,
                CodeChallenge: null,
                CodeChallengeMethod: null,
                Prompt: null),
            Now + TimeSpan.FromMinutes(10),
            TimeSpan.FromSeconds(5)));
        _pending.Approve(issued.Handle, "jperez", "user-1", Now);

        var granted = await new DeviceCodeGrantHandler(_pending, UserStore, RefreshTokens, Tokens, Clock)
            .HandleAsync(new TokenRequest(
                Clients.Find(ClientStoreFixture.SpaClientId)!,
                Issuer,
                ["email"],
                Code: null,
                RedirectUri: null,
                CodeVerifier: null,
                RefreshToken: null,
                UserName: null,
                Password: null,
                DeviceCode: issued.Handle));

        Assert.True(granted.Succeeded, granted.Error?.ToString());
        Assert.Null(granted.Value!.IdToken);
    }

    private TokenRequest PasswordRequest(IReadOnlyList<string> scopes) =>
        new(
            Clients.Find(ClientStoreFixture.ServiceClientId)!,
            Issuer,
            scopes,
            Code: null,
            RedirectUri: null,
            CodeVerifier: null,
            RefreshToken: null,
            UserName: "jperez",
            Password: "clave");

    private Core.Errors.Result<TokenResponse> Issue(
        IReadOnlyList<string> scopes,
        string? nonce = null,
        bool includeIdToken = true,
        bool includeRefreshToken = true,
        string? refreshTokenFamilyId = null,
        Client? forClient = null,
        IRefreshTokenStore? into = null) =>
        TokenResponseFactory.Issue(
            Tokens,
            into ?? RefreshTokens,
            Issuer,
            forClient ?? Clients.Find(ClientStoreFixture.SpaClientId)!,
            SampleUser(),
            scopes,
            Now,
            nonce,
            authorizationCode: null,
            includeIdToken,
            includeRefreshToken,
            refreshTokenFamilyId);
}