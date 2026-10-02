using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Tokens;

namespace OidcMock.UnitTests.Grants;

public sealed class ClientCredentialsGrantHandlerTests : GrantHandlerTestBase
{
    private const string ClientId = "backend-service";

    private readonly ClientCredentialsGrantHandler _handler;

    public ClientCredentialsGrantHandlerTests() => _handler = new(Tokens);

    [Fact]
    public async Task EmiteAccessTokenParaElPropioCliente()
    {
        var response = await HandleAsync();

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.Equal(ClientId, TokenTestValidator.ReadClaim(response.Value!.AccessToken, "sub").GetString());
    }

    [Fact]
    public async Task ElAccessTokenEsValidoContraElJwks()
    {
        var response = await HandleAsync();

        var result = Validator.Validate(response.Value!.AccessToken, Issuer, [ClientId]);

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task NoEmiteIdTokenNiRefreshToken()
    {
        var response = await HandleAsync();

        Assert.Null(response.Value?.IdToken);
        Assert.Null(response.Value?.RefreshToken);
    }

    [Fact]
    public async Task SinUsuarioDetrasNoProyectaClaimsDeUsuario()
    {
        var response = await HandleAsync(scopes: ["openid", "custom.profile"]);

        var claims = TokenTestValidator.ReadClaimNames(response.Value!.AccessToken).ToList();

        Assert.DoesNotContain("full_name", claims);
        Assert.DoesNotContain("email", claims);
        Assert.Equal("backend-service", TokenTestValidator.ReadClaim(response.Value.AccessToken, "sub").GetString());
    }

    private async Task<Result<TokenResponse>> HandleAsync(IReadOnlyList<string>? scopes = null) =>
        await _handler.HandleAsync(new TokenRequest(
            ClientStoreFixture.Service(),
            Issuer,
            scopes ?? ["openid"],
            null,
            null,
            null,
            null,
            null,
            null));
}

public sealed class PasswordGrantHandlerTests : GrantHandlerTestBase
{
    private readonly PasswordGrantHandler _handler;

    public PasswordGrantHandlerTests() =>
        _handler = new PasswordGrantHandler(UserStore, RefreshTokens, Tokens, Clock);

    [Fact]
    public async Task EmiteTokensConCredencialesCorrectas()
    {
        var response = await HandleAsync("jperez", "clave");

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.NotNull(response.Value?.AccessToken);
        Assert.NotNull(response.Value?.IdToken);
        Assert.NotNull(response.Value?.RefreshToken);
    }

    [Fact]
    public async Task ElAccessTokenLlevaElSubjectDelUsuario()
    {
        var response = await HandleAsync("jperez", "clave");

        Assert.Equal("user-1", TokenTestValidator.ReadClaim(response.Value!.AccessToken, "sub").GetString());
    }

    [Fact]
    public async Task RechazaUnaContrasenaIncorrecta()
    {
        Assert.Equal("invalid_grant", (await HandleAsync("jperez", "clave-incorrecta")).Error?.Code);
    }

    [Fact]
    public async Task RechazaUnUsuarioDesconocido()
    {
        Assert.Equal("invalid_grant", (await HandleAsync("nadie", "clave")).Error?.Code);
    }

    [Fact]
    public async Task ExigeUsuarioYContrasena()
    {
        Assert.Equal("invalid_request", (await HandleAsync(null, "clave")).Error?.Code);
        Assert.Equal("invalid_request", (await HandleAsync("jperez", null)).Error?.Code);
    }

    private async Task<Result<TokenResponse>> HandleAsync(string? userName, string? password) =>
        await _handler.HandleAsync(new TokenRequest(
            ClientStoreFixture.Service(),
            Issuer,
            ["openid"],
            null,
            null,
            null,
            null,
            userName,
            password));
}
public sealed class RefreshTokenGrantHandlerTests : GrantHandlerTestBase
{
    private readonly RefreshTokenGrantHandler _handler;

    public RefreshTokenGrantHandlerTests() =>
        _handler = new RefreshTokenGrantHandler(RefreshTokens, UserStore, Tokens, Clock);

    [Fact]
    public async Task CanjeaUnRefreshTokenValidoYEmiteTokensNuevos()
    {
        var response = (await HandleAsync(IssueRefreshToken().Token));

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.NotNull(response.Value?.AccessToken);
    }

    [Fact]
    public async Task ElRefreshTokenNuevoEsDistintoDelCanjeado()
    {
        var token = IssueRefreshToken();

        var response = await HandleAsync(token.Token);

        Assert.NotEqual(token.Token, response.Value?.RefreshToken);
    }

    [Fact]
    public async Task ConservaLosScopesConcedidosEnElRefreshTokenOriginal()
    {
        var response = await HandleAsync(IssueRefreshToken(scopes: ["openid", "email"]).Token);

        Assert.Equal("openid email", response.Value?.Scope);
    }

    [Fact]
    public async Task RechazaUnRefreshTokenDesconocido()
    {
        Assert.Equal("invalid_grant", (await HandleAsync("inventado")).Error?.Code);
    }

    [Fact]
    public async Task RechazaUnRefreshTokenYaCanjeado()
    {
        var token = IssueRefreshToken();
        await HandleAsync(token.Token);

        Assert.Equal("invalid_grant", (await HandleAsync(token.Token)).Error?.Code);
    }

    [Fact]
    public async Task RechazaUnRefreshTokenEmitidoParaOtroCliente()
    {
        var token = RefreshTokens.Issue(new RefreshTokenRequest(
            "backend-service",
            "user-1",
            ["openid"],
            TimeSpan.FromHours(1)));

        Assert.Equal("invalid_grant", (await HandleAsync(token.Token)).Error?.Code);
    }

    [Fact]
    public async Task ElRefreshNoReemiteIdTokenPorDisenoDelMock()
    {
        var response = (await HandleAsync(IssueRefreshToken().Token));

        Assert.Null(response.Value?.IdToken);
    }

    private RefreshToken IssueRefreshToken(IReadOnlyList<string>? scopes = null) =>
        RefreshTokens.Issue(new RefreshTokenRequest(
            "web-app-spa",
            "user-1",
            scopes ?? ["openid"],
            TimeSpan.FromHours(1)));

    private async Task<Result<TokenResponse>> HandleAsync(string token) =>
        await _handler.HandleAsync(new TokenRequest(
            ClientStoreFixture.Spa(),
            Issuer,
            ["openid"],
            null,
            null,
            null,
            token,
            null,
            null));
}
