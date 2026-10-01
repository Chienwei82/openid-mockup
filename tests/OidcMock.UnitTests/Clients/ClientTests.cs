using OidcMock.Core.Clients;

namespace OidcMock.UnitTests.Clients;

public sealed class ClientTests
{
    private const string GrantType = "authorization_code";
    private const string RedirectUri = "https://app.local/callback";
    private const string Scope = "openid";

    private static readonly TokenLifetimes DefaultLifetimes = new(
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(8),
        TimeSpan.FromMinutes(5));

    [Fact]
    public void AceptaUnGrantTypeRegistrado()
    {
        var client = CreateClient(allowedGrantTypes: [GrantType]);

        Assert.True(client.AllowsGrantType(GrantType));
    }

    [Fact]
    public void RechazaUnGrantTypeNoRegistrado()
    {
        var client = CreateClient(allowedGrantTypes: [GrantType]);

        Assert.False(client.AllowsGrantType("client_credentials"));
    }

    [Fact]
    public void AceptaUnaRedirectUriRegistrada()
    {
        var client = CreateClient(redirectUris: [RedirectUri]);

        Assert.True(client.AllowsRedirectUri(RedirectUri));
    }

    [Fact]
    public void RechazaUnaRedirectUriNoRegistrada()
    {
        var client = CreateClient(redirectUris: [RedirectUri]);

        Assert.False(client.AllowsRedirectUri("https://otro.local/callback"));
    }

    [Fact]
    public void AceptaUnScopeRegistrado()
    {
        var client = CreateClient(allowedScopes: [Scope]);

        Assert.True(client.AllowsScope(Scope));
    }

    [Fact]
    public void ExigeSecretoCuandoElClienteEsConfidencial()
    {
        var client = CreateClient(requireClientSecret: true);

        Assert.True(client.RequireClientSecret);
    }

    private static Client CreateClient(
        IReadOnlyList<string>? redirectUris = null,
        IReadOnlyList<string>? allowedGrantTypes = null,
        IReadOnlyList<string>? allowedScopes = null,
        bool requireClientSecret = true) => new(
            ClientId: "cliente-prueba",
            ClientSecret: "secreto",
            RedirectUris: redirectUris ?? [],
            PostLogoutRedirectUris: [],
            AllowedGrantTypes: allowedGrantTypes ?? [],
            AllowedScopes: allowedScopes ?? [],
            RequirePkce: true,
            RequireClientSecret: requireClientSecret,
            TokenLifetimes: DefaultLifetimes,
            Branding: new Branding("Cliente de prueba", null, null));
}
