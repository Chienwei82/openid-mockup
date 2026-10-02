using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Tokens;

namespace OidcMock.UnitTests.Grants;

/// <summary>
/// Grant refresh_token (RFC 6749 6) con rotacion obligatoria, scope que solo puede Narrowing,
/// deteccion de reutilizacion con revocacion de la familia y caducidad por cliente.
/// </summary>
public sealed class RefreshTokenGrantHandlerTests : GrantHandlerTestBase
{
    private readonly RefreshTokenGrantHandler _handler;

    public RefreshTokenGrantHandlerTests() =>
        _handler = new RefreshTokenGrantHandler(RefreshTokens, UserStore, Tokens, Clock, Revocations);

    /// <summary>
    /// El store de revocaciones es la memoria compartida de "este token ya no vale": aunque el token
    /// siguiera en el store de refresh tokens, una familia revocada no se puede canjear.
    /// </summary>
    [Fact]
    public async Task RechazaUnRefreshTokenDeUnaFamiliaRevocada()
    {
        var token = IssueRefreshToken();
        Revocations.RevokeRefreshTokenFamily(token.FamilyId, Clock.GetUtcNow().AddHours(8));

        var response = await HandleAsync(token.Token);

        Assert.Equal("invalid_grant", response.Error?.Code);
    }

    [Fact]
    public async Task LaRevocacionDeUnaFamiliaNoAfectaAOtra()
    {
        var revocada = IssueRefreshToken();
        var vigente = IssueRefreshToken();
        Revocations.RevokeRefreshTokenFamily(revocada.FamilyId, Clock.GetUtcNow().AddHours(8));

        Assert.Equal("invalid_grant", (await HandleAsync(revocada.Token)).Error?.Code);
        Assert.True((await HandleAsync(vigente.Token)).Succeeded);
    }

    [Fact]
    public async Task CanjeaUnRefreshTokenValidoYEmiteTokensNuevos()
    {
        var response = await HandleAsync(IssueRefreshToken().Token);

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.NotNull(response.Value?.AccessToken);
        Assert.NotNull(response.Value?.RefreshToken);
    }

    [Fact]
    public async Task CadaCanjeEmiteUnRefreshTokenDistintoYAnulaElAnterior()
    {
        var primero = IssueRefreshToken();

        var segundo = (await HandleAsync(primero.Token)).Value!.RefreshToken!;
        var tercero = (await HandleAsync(segundo)).Value!.RefreshToken!;

        Assert.NotEqual(primero.Token, segundo);
        Assert.NotEqual(segundo, tercero);
    }

    [Fact]
    public async Task DetectaLaReutilizacionDeUnTokenYaRotado()
    {
        var original = IssueRefreshToken();
        var rotado = (await HandleAsync(original.Token)).Value!.RefreshToken!;

        var reutilizado = await HandleAsync(original.Token);

        Assert.Equal("invalid_grant", reutilizado.Error?.Code);
        Assert.NotNull(rotado);
    }

    [Fact]
    public async Task LaReutilizacionRevocaLaFamiliaCompleta()
    {
        var original = IssueRefreshToken();
        var rotado = (await HandleAsync(original.Token)).Value!.RefreshToken!;

        await HandleAsync(original.Token);

        Assert.Equal("invalid_grant", (await HandleAsync(rotado)).Error?.Code);
    }

    [Fact]
    public async Task LaRevocacionDeLaFamiliaNoAfectaOtraFamiliaDelMismoCliente()
    {
        var familiaA = IssueRefreshToken();
        var familiaB = IssueRefreshToken();
        var rotado = (await HandleAsync(familiaA.Token)).Value!.RefreshToken!;

        await HandleAsync(familiaA.Token);

        Assert.Equal("invalid_grant", (await HandleAsync(rotado)).Error?.Code);
        Assert.True((await HandleAsync(familiaB.Token)).Succeeded);
    }

    [Fact]
    public async Task AceptaReducirElScopeDelRefreshTokenOriginal()
    {
        var token = IssueRefreshToken(scopes: ["openid", "email", "roles"]);

        var response = await HandleAsync(token.Token, requestedScopes: ["openid", "email"]);

        Assert.Equal("openid email", response.Value?.Scope);
    }

    [Fact]
    public async Task RechazaAmpliarElScopeOriginalConInvalidScope()
    {
        var token = IssueRefreshToken(scopes: ["openid"]);

        var response = await HandleAsync(token.Token, requestedScopes: ["openid", "email"]);

        Assert.Equal("invalid_scope", response.Error?.Code);
    }

    [Fact]
    public async Task ElScopeAmpliadoNoEmiteTokens()
    {
        var token = IssueRefreshToken(scopes: ["openid"]);

        var response = await HandleAsync(token.Token, requestedScopes: ["openid", "email"]);

        Assert.Null(response.Value?.AccessToken);
        Assert.Null(response.Value?.RefreshToken);
    }

    [Fact]
    public async Task ConservaLosScopesDelRefreshTokenCuandoNoSePidieron()
    {
        var token = IssueRefreshToken(scopes: ["openid", "email"]);

        var response = await HandleAsync(token.Token);

        Assert.Equal("openid email", response.Value?.Scope);
    }

    [Fact]
    public async Task CaducaElRefreshTokenSegunElLifetimeDelCliente()
    {
        var token = IssueRefreshToken();

        Clock.Advance(ClientStoreFixture.Spa().TokenLifetimes.RefreshToken);

        Assert.Equal("invalid_grant", (await HandleAsync(token.Token)).Error?.Code);
    }

    [Fact]
    public async Task AntesDeCaducarElRefreshTokenSigueSiendoValido()
    {
        var token = IssueRefreshToken();

        Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.True((await HandleAsync(token.Token)).Succeeded);
    }

    [Fact]
    public async Task RechazaUnRefreshTokenDesconocido()
    {
        Assert.Equal("invalid_grant", (await HandleAsync("inventado")).Error?.Code);
    }

    [Fact]
    public async Task ElRefreshNoReemiteIdTokenPorDisenoDelMock()
    {
        var response = await HandleAsync(IssueRefreshToken().Token);

        Assert.Null(response.Value?.IdToken);
    }

    private RefreshToken IssueRefreshToken(
        string clientId = ClientStoreFixture.SpaClientId,
        IReadOnlyList<string>? scopes = null) =>
        RefreshTokens.Issue(new RefreshTokenRequest(
            clientId,
            "user-1",
            scopes ?? ["openid"],
            ClientStoreFixture.Spa().TokenLifetimes.RefreshToken));

    private async Task<Result<TokenResponse>> HandleAsync(
        string token,
        IReadOnlyList<string>? requestedScopes = null) =>
        await _handler.HandleAsync(new TokenRequest(
            ClientStoreFixture.Spa(),
            Issuer,
            requestedScopes ?? [],
            null,
            null,
            null,
            token,
            null,
            null,
            ScopesRequested: requestedScopes is not null));
}