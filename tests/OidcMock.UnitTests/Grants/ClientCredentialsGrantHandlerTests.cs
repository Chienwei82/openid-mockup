using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Tokens;

namespace OidcMock.UnitTests.Grants;

/// <summary>
/// Grant client_credentials (RFC 6749 4.4): solo para clientes confidenciales, sin usuario detras,
/// con sub igual al client_id y sin id_token ni refresh token.
/// </summary>
public sealed class ClientCredentialsGrantHandlerTests : GrantHandlerTestBase
{
    private readonly ClientCredentialsGrantHandler _handler;

    public ClientCredentialsGrantHandlerTests() => _handler = new(Tokens);

    [Fact]
    public async Task EmiteAccessTokenParaElPropioCliente()
    {
        var response = await HandleAsync();

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.Equal(
            ClientStoreFixture.ServiceClientId,
            TokenTestValidator.ReadClaim(response.Value!.AccessToken, "sub").GetString());
    }

    [Fact]
    public async Task ElAccessTokenEsValidoContraElJwks()
    {
        var response = await HandleAsync();

        var result = Validator.Validate(
            response.Value!.AccessToken,
            Issuer,
            [ClientStoreFixture.ServiceClientId]);

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
    public async Task AnunciaBearerYElLifetimeDeAccessTokenDelCliente()
    {
        var response = await HandleAsync();

        Assert.Equal("Bearer", response.Value?.TokenType);
        Assert.Equal(
            (long)ClientStoreFixture.Service().TokenLifetimes.AccessToken.TotalSeconds,
            response.Value?.ExpiresIn);
    }

    [Fact]
    public async Task SinUsuarioDetrasNoProyectaClaimsDeUsuario()
    {
        var response = await HandleAsync(scopes: ["openid", "email"]);

        var claims = TokenTestValidator.ReadClaimNames(response.Value!.AccessToken).ToList();

        Assert.DoesNotContain("full_name", claims);
        Assert.DoesNotContain("email", claims);
    }

    [Fact]
    public async Task RechazaUnClientePublico()
    {
        var publico = ClientStoreFixture.Spa() with
        {
            AllowedGrantTypes = [GrantTypes.ClientCredentials]
        };

        var response = await HandleAsync(publico);

        Assert.Equal("invalid_client", response.Error?.Code);
    }

    [Fact]
    public async Task AceptaUnClienteConfidencialQueTieneSecreto()
    {
        var response = await HandleAsync();

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.NotNull(ClientStoreFixture.Service().ClientSecret);
    }

    private async Task<Result<TokenResponse>> HandleAsync(Client? client = null, IReadOnlyList<string>? scopes = null) =>
        await _handler.HandleAsync(new TokenRequest(
            client ?? ClientStoreFixture.Service(),
            Issuer,
            scopes ?? ["openid"],
            null,
            null,
            null,
            null,
            null,
            null));
}