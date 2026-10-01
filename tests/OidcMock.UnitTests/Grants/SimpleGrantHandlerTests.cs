using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Tokens;

namespace OidcMock.UnitTests.Grants;

public sealed class ClientCredentialsGrantHandlerTests : GrantHandlerTestBase
{
    private const string ClientId = "backend-service";

    private readonly ClientCredentialsGrantHandler _handler;

    public ClientCredentialsGrantHandlerTests() => _handler = new(Tokens, Clock);

    [Fact]
    public void EmiteAccessTokenParaElPropioCliente()
    {
        var response = Handle();

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.Equal(ClientId, TokenTestValidator.ReadClaim(response.Value!.AccessToken, "sub").GetString());
    }

    [Fact]
    public void ElAccessTokenEsValidoContraElJwks()
    {
        var response = Handle();

        var result = Validator.Validate(response.Value!.AccessToken, Issuer, [ClientId]);

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public void NoEmiteIdTokenNiRefreshToken()
    {
        var response = Handle();

        Assert.Null(response.Value?.IdToken);
        Assert.Null(response.Value?.RefreshToken);
    }

    [Fact]
    public void SinUsuarioDetrasNoProyectaClaimsDeUsuario()
    {
        var response = Handle(scopes: ["openid", "custom.profile"]);

        var claims = TokenTestValidator.ReadClaimNames(response.Value!.AccessToken).ToList();

        Assert.DoesNotContain("full_name", claims);
        Assert.DoesNotContain("email", claims);
        Assert.Equal("backend-service", TokenTestValidator.ReadClaim(response.Value.AccessToken, "sub").GetString());
    }

    private Result<TokenResponse> Handle(IReadOnlyList<string>? scopes = null) =>
        _handler.Handle(new TokenRequest(
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
    public void EmiteTokensConCredencialesCorrectas()
    {
        var response = Handle("jperez", "clave");

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.NotNull(response.Value?.AccessToken);
        Assert.NotNull(response.Value?.IdToken);
        Assert.NotNull(response.Value?.RefreshToken);
    }

    [Fact]
    public void ElAccessTokenLlevaElSubjectDelUsuario()
    {
        var response = Handle("jperez", "clave");

        Assert.Equal("user-1", TokenTestValidator.ReadClaim(response.Value!.AccessToken, "sub").GetString());
    }

    [Fact]
    public void RechazaUnaContrasenaIncorrecta()
    {
        Assert.Equal("invalid_grant", Handle("jperez", "clave-incorrecta").Error?.Code);
    }

    [Fact]
    public void RechazaUnUsuarioDesconocido()
    {
        Assert.Equal("invalid_grant", Handle("nadie", "clave").Error?.Code);
    }

    [Fact]
    public void ExigeUsuarioYContrasena()
    {
        Assert.Equal("invalid_request", Handle(null, "clave").Error?.Code);
        Assert.Equal("invalid_request", Handle("jperez", null).Error?.Code);
    }

    private Result<TokenResponse> Handle(string? userName, string? password) =>
        _handler.Handle(new TokenRequest(
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
    public void CanjeaUnRefreshTokenValidoYEmiteTokensNuevos()
    {
        var response = Handle(IssueRefreshToken().Token);

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.NotNull(response.Value?.AccessToken);
    }

    [Fact]
    public void ElRefreshTokenNuevoEsDistintoDelCanjeado()
    {
        var token = IssueRefreshToken();

        var response = Handle(token.Token);

        Assert.NotEqual(token.Token, response.Value?.RefreshToken);
    }

    [Fact]
    public void ConservaLosScopesConcedidosEnElRefreshTokenOriginal()
    {
        var response = Handle(IssueRefreshToken(scopes: ["openid", "email"]).Token);

        Assert.Equal("openid email", response.Value?.Scope);
    }

    [Fact]
    public void RechazaUnRefreshTokenDesconocido()
    {
        Assert.Equal("invalid_grant", Handle("inventado").Error?.Code);
    }

    [Fact]
    public void RechazaUnRefreshTokenYaCanjeado()
    {
        var token = IssueRefreshToken();
        Handle(token.Token);

        Assert.Equal("invalid_grant", Handle(token.Token).Error?.Code);
    }

    [Fact]
    public void RechazaUnRefreshTokenEmitidoParaOtroCliente()
    {
        var token = RefreshTokens.Issue(new RefreshTokenRequest(
            "backend-service",
            "user-1",
            ["openid"],
            TimeSpan.FromHours(1)));

        Assert.Equal("invalid_grant", Handle(token.Token).Error?.Code);
    }

    [Fact]
    public void ElRefreshNoReemiteIdTokenPorDisenoDelMock()
    {
        var response = Handle(IssueRefreshToken().Token);

        Assert.Null(response.Value?.IdToken);
    }

    private RefreshToken IssueRefreshToken(IReadOnlyList<string>? scopes = null) =>
        RefreshTokens.Issue(new RefreshTokenRequest(
            "web-app-spa",
            "user-1",
            scopes ?? ["openid"],
            TimeSpan.FromHours(1)));

    private Result<TokenResponse> Handle(string token) =>
        _handler.Handle(new TokenRequest(
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
