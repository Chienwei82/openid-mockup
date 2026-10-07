using OidcMock.Core.Authorization;
using OidcMock.Core.Clients;
using OidcMock.Core.Codes;
using OidcMock.Core.Grants;

namespace OidcMock.UnitTests.Authorization;

/// <summary>
/// El compositor de la URL de authorize que la pantalla raiz muestra para copiar: arma la peticion a
/// connect/authorize/callback con los valores del cliente precargados y, cuando el cliente exige PKCE,
/// un par code_verifier/code_challenge para que la prueba se pueda canjear.
/// </summary>
public sealed class AuthorizeUrlComposerTests
{
    private const string Issuer = "https://miserver/oidc/";
    private const string RedirectUri = "https://localhost:5173/callback";

    [Fact]
    public void ComponeLaUrlDelCallbackConLosParametrosBase()
    {
        var result = AuthorizeUrlComposer.Compose(Issuer, Client(requirePkce: false));

        Assert.StartsWith("https://miserver/oidc/connect/authorize/callback?", result.Url, StringComparison.Ordinal);
        Assert.Contains("client_id=web-app-spa", result.Url, StringComparison.Ordinal);
        Assert.Contains("response_type=code", result.Url, StringComparison.Ordinal);
        Assert.Contains("state=state-de-prueba", result.Url, StringComparison.Ordinal);
        Assert.Contains("nonce=nonce-de-prueba", result.Url, StringComparison.Ordinal);
        Assert.Contains($"redirect_uri={Uri.EscapeDataString(RedirectUri)}", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void ElScopeSaleDeLosEstandaresPermitidosPorElCliente()
    {
        var result = AuthorizeUrlComposer.Compose(Issuer, Client(requirePkce: false));

        Assert.Contains("scope=openid%20profile%20email%20offline_access", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void AnadePkceConElChallengeDelVerifierCuandoElClienteLoExige()
    {
        var result = AuthorizeUrlComposer.Compose(Issuer, Client(requirePkce: true));

        Assert.NotNull(result.CodeVerifier);
        Assert.Contains("code_challenge_method=S256", result.Url, StringComparison.Ordinal);
        Assert.Contains(
            $"code_challenge={CodeChallenges.Create(PkceCodeChallengeMethods.Sha256, result.CodeVerifier!)}",
            result.Url,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NoAnadePkceCuandoElClienteNoLoExige()
    {
        var result = AuthorizeUrlComposer.Compose(Issuer, Client(requirePkce: false));

        Assert.Null(result.CodeVerifier);
        Assert.DoesNotContain("code_challenge", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void UsaElPrimerRedirectUriRegistrado()
    {
        var client = Client(requirePkce: false) with { RedirectUris = [RedirectUri, "https://localhost:5173/otro"] };

        var result = AuthorizeUrlComposer.Compose(Issuer, client);

        Assert.Contains($"redirect_uri={Uri.EscapeDataString(RedirectUri)}", result.Url, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString("https://localhost:5173/otro"), result.Url, StringComparison.Ordinal);
    }

    private static Client Client(bool requirePkce) => new(
        ClientId: "web-app-spa",
        ClientSecret: null,
        RedirectUris: [RedirectUri],
        PostLogoutRedirectUris: [],
        AllowedGrantTypes: [GrantTypes.AuthorizationCode],
        AllowedScopes: ["openid", "profile", "email", "nombre", "offline_access"],
        RequirePkce: requirePkce,
        RequireClientSecret: false,
        TokenLifetimes: new TokenLifetimes(
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(8),
            TimeSpan.FromMinutes(5)),
        Branding: new Branding("OidcMock - Persona Física", "/assets/logo-mock.svg", "#00695C"));
}
