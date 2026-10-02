using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Tokens;

namespace OidcMock.UnitTests.Grants;

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
