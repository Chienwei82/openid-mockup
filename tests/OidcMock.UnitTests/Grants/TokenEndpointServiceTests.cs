using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Grants;

namespace OidcMock.UnitTests.Grants;

/// <summary>
/// Autenticacion del cliente, despacho por grant y validacion de scopes del token endpoint.
/// </summary>
public sealed class TokenEndpointServiceTests : GrantHandlerTestBase
{
    private readonly TokenEndpointService _service;

    public TokenEndpointServiceTests()
    {
        var registry = new GrantHandlerRegistry(
        [
            new ClientCredentialsGrantHandler(Tokens),
            new PasswordGrantHandler(UserStore, RefreshTokens, Tokens, Clock)
        ]);

        _service = ServiceOver(ClientStoreFixture.Create(), registry);
    }

    private static TokenEndpointService ServiceOver(
        IClientStore clientStore,
        GrantHandlerRegistry registry) =>
        new(
            ScopeStoreFixture.Create(),
            ClientStoreFixture.AuthenticatorOver(clientStore),
            registry);

    [Fact]
    public async Task EmiteTokensDeClientCredentialsConElSecretoCorrecto()
    {
        var response = await IssueAsync(clientId: "backend-service", clientSecret: "super-secret-backend");

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.NotNull(response.Value?.AccessToken);
    }

    [Fact]
    public async Task UnClienteSinSecretoNoLoExigeEnElTokenEndpoint()
    {
        var spa = ClientStoreFixture.Spa() with
        {
            RequireClientSecret = false,
            AllowedGrantTypes = [GrantTypes.Password]
        };
        var service = ServiceOver(
            new SingleClientStore(spa),
            new GrantHandlerRegistry([new PasswordGrantHandler(UserStore, RefreshTokens, Tokens, Clock)]));

        var response = await service.IssueTokenAsync(
            new TokenEndpointRequest(
                new ClientCredentials(
                    ClientAuthenticationMethods.ClientSecretPost,
                    spa.ClientId,
                    ClientSecret: null),
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
    public async Task RechazaUnSecretoIncorrectoConInvalidClientY401()
    {
        var response = await IssueAsync(clientId: "backend-service", clientSecret: "secreto-malo");

        Assert.Equal("invalid_client", response.Error?.Code);
        Assert.Equal(401, response.Error?.StatusCode);
    }

    [Fact]
    public async Task RechazaUnClienteDesconocido()
    {
        var response = await IssueAsync(clientId: "cliente-fantasma", clientSecret: "lo-que-sea");

        Assert.Equal("invalid_client", response.Error?.Code);
    }

    [Fact]
    public async Task RechazaUnSecretoAusenteEnUnClienteConfidencial()
    {
        var response = await IssueAsync(clientId: "backend-service", clientSecret: null);

        Assert.Equal("invalid_client", response.Error?.Code);
    }

    [Fact]
    public async Task RechazaUnGrantTypeDesconocido()
    {
        var response = await IssueAsync(clientId: "backend-service", clientSecret: "super-secret-backend", grantType: "inventado");

        Assert.Equal("unsupported_grant_type", response.Error?.Code);
    }

    [Fact]
    public async Task RechazaUnGrantTypeQueElClienteNoTienePermitido()
    {
        var response = await IssueAsync(
            clientId: "backend-service",
            clientSecret: "super-secret-backend",
            grantType: "authorization_code");

        Assert.Equal("unsupported_grant_type", response.Error?.Code);
    }

    [Fact]
    public async Task RechazaUnScopeNoPermitidoParaElCliente()
    {
        var response = await IssueAsync(
            clientId: "backend-service",
            clientSecret: "super-secret-backend",
            scopes: ["openid", "documentofva"]);

        Assert.Equal("invalid_scope", response.Error?.Code);
    }

    [Fact]
    public async Task SinScopesUsaOpenid()
    {
        var response = await IssueAsync(clientId: "backend-service", clientSecret: "super-secret-backend", scopes: []);

        Assert.Equal("openid", response.Value?.Scope);
    }

    [Fact]
    public async Task LaAutenticacionSeCompruebaAntesDeDespacharAlGrant()
    {
        var response = await IssueAsync(
            clientId: "backend-service",
            clientSecret: "secreto-malo",
            grantType: "grant-inventado-que-tambien-fallaria");

        Assert.Equal("invalid_client", response.Error?.Code);
    }

    private async Task<Result<TokenResponse>> IssueAsync(
        string? clientId,
        string? clientSecret,
        string grantType = "client_credentials",
        IReadOnlyList<string>? scopes = null,
        string? userName = null,
        string? password = null) =>
        await _service.IssueTokenAsync(
            new TokenEndpointRequest(
                new ClientCredentials(
                    ClientAuthenticationMethods.ClientSecretPost,
                    clientId,
                    clientSecret),
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