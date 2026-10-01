using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Grants;

namespace OidcMock.UnitTests.Grants;

public sealed class TokenEndpointServiceTests : GrantHandlerTestBase
{
    private readonly TokenEndpointService _service;

    public TokenEndpointServiceTests()
    {
        var registry = new GrantHandlerRegistry(
        [
            new ClientCredentialsGrantHandler(Tokens, Clock),
            new PasswordGrantHandler(UserStore, RefreshTokens, Tokens, Clock)
        ]);

        _service = new TokenEndpointService(ClientStoreFixture.Create(), ScopeStoreFixture.Create(), registry);
    }

    [Fact]
    public void EmiteTokensDeClientCredentialsConElSecretoCorrecto()
    {
        var response = Issue(clientId: "backend-service", clientSecret: "super-secret-backend");

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.NotNull(response.Value?.AccessToken);
    }

    [Fact]
    public void UnClienteSinSecretoNoLoExigeEnElTokenEndpoint()
    {
        var spa = ClientStoreFixture.Spa() with
        {
            RequireClientSecret = false,
            AllowedGrantTypes = [GrantTypes.Password]
        };
        var service = new TokenEndpointService(
            new SingleClientStore(spa),
            ScopeStoreFixture.Create(),
            new GrantHandlerRegistry([new PasswordGrantHandler(UserStore, RefreshTokens, Tokens, Clock)]));

        var response = service.IssueToken(
            new TokenEndpointRequest(
                spa.ClientId,
                ClientSecret: null,
                "password",
                ["openid"],
                null,
                null,
                null,
                null,
                "jperez",
                "clave"),
            Issuer);

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.NotNull(response.Value?.AccessToken);
    }

    [Fact]
    public void RechazaUnSecretoIncorrectoConInvalidClientY401()
    {
        var response = Issue(clientId: "backend-service", clientSecret: "secreto-malo");

        Assert.Equal("invalid_client", response.Error?.Code);
        Assert.Equal(401, response.Error?.StatusCode);
    }

    [Fact]
    public void RechazaUnClienteDesconocido()
    {
        var response = Issue(clientId: "cliente-fantasma", clientSecret: "lo-que-sea");

        Assert.Equal("invalid_client", response.Error?.Code);
    }

    [Fact]
    public void RechazaUnSecretoAusenteEnUnClienteConfidencial()
    {
        var response = Issue(clientId: "backend-service", clientSecret: null);

        Assert.Equal("invalid_client", response.Error?.Code);
    }

    [Fact]
    public void RechazaUnGrantTypeDesconocido()
    {
        var response = Issue(clientId: "backend-service", clientSecret: "super-secret-backend", grantType: "inventado");

        Assert.Equal("unsupported_grant_type", response.Error?.Code);
    }

    [Fact]
    public void RechazaUnGrantTypeQueElClienteNoTienePermitido()
    {
        var response = Issue(
            clientId: "backend-service",
            clientSecret: "super-secret-backend",
            grantType: "authorization_code");

        Assert.Equal("unsupported_grant_type", response.Error?.Code);
    }

    [Fact]
    public void RechazaUnScopeNoPermitidoParaElCliente()
    {
        var response = Issue(
            clientId: "backend-service",
            clientSecret: "super-secret-backend",
            scopes: ["openid", "documentofva"]);

        Assert.Equal("invalid_scope", response.Error?.Code);
    }

    [Fact]
    public void SinScopesUsaOpenid()
    {
        var response = Issue(clientId: "backend-service", clientSecret: "super-secret-backend", scopes: []);

        Assert.Equal("openid", response.Value?.Scope);
    }

    [Fact]
    public void LaAutenticacionSeCompruebaAntesDeDespacharAlGrant()
    {
        var response = Issue(
            clientId: "backend-service",
            clientSecret: "secreto-malo",
            grantType: "grant-inventado-que-tambien-fallaria");

        Assert.Equal("invalid_client", response.Error?.Code);
    }

    private Result<TokenResponse> Issue(
        string? clientId,
        string? clientSecret,
        string grantType = "client_credentials",
        IReadOnlyList<string>? scopes = null,
        string? userName = null,
        string? password = null) =>
        _service.IssueToken(
            new TokenEndpointRequest(
                clientId,
                clientSecret,
                grantType,
                scopes ?? ["openid"],
                null,
                null,
                null,
                null,
                userName,
                password),
            Issuer);

    private sealed class SingleClientStore(Client client) : IClientStore
    {
        public Client? Find(string clientId) =>
            string.Equals(client.ClientId, clientId, StringComparison.Ordinal) ? client : null;

        public IReadOnlyList<Client> List() => [client];
    }
}