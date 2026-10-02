using OidcMock.Core.Authorization;
using OidcMock.Core.Codes;
using OidcMock.Core.Users;
using Microsoft.Extensions.Time.Testing;
using OidcMock.UnitTests.Fixtures;

namespace OidcMock.UnitTests.Authorization;

/// <summary>
/// Tests de la pantalla de login: valida credenciales y emite el codigo. Cada test fija un dato que
/// el canje posterior necesita, porque un codigo incompleto rompe el flujo entero mas adelante y el
/// fallo aparece lejos de su causa.
/// </summary>
public sealed class AuthorizationServiceTests
{
    private const string RedirectUri = "https://localhost:5173/callback";
    private const string UserName = "jperez";
    private const string Password = "clave";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryCodeStore _codeStore;
    private readonly AuthorizationService _service;

    public AuthorizationServiceTests()
    {
        _codeStore = new InMemoryCodeStore(_clock);
        _service = new AuthorizationService(_codeStore, new SingleUserStore());
    }

    [Fact]
    public void CredencialesCorrectasEmitenElCodigoDeAutorizacion()
    {
        var granted = SignIn();

        Assert.True(granted.Succeeded, granted.Error?.ToString());
        Assert.False(string.IsNullOrWhiteSpace(granted.Value?.Code.Code));
    }

    /// <summary>
    /// Regresion: el codigo debe llevar el redirect_uri con el que se aprobo. Sin el, el canje en el
    /// token endpoint falla con invalid_grant porque no puede verificar la coincidencia exigida por
    /// RFC 6749 4.1.3.
    /// </summary>
    [Fact]
    public void ElCodigoGuardaElRedirectUriDeLaPeticionAprobada()
    {
        var granted = SignIn();

        Assert.Equal(RedirectUri, granted.Value?.Code.RedirectUri);
    }

    [Fact]
    public void ElCodigoGuardaElClientIdYLosScopesAprobados()
    {
        var granted = SignIn(scopes: ["openid", "email"]);

        Assert.Equal(ClientStoreFixture.SpaClientId, granted.Value?.Code.ClientId);
        Assert.Equal(["openid", "email"], granted.Value?.Code.Scopes);
    }

    [Fact]
    public void ElCodigoGuardaNonceStateYCodigoPkce()
    {
        var granted = SignIn(nonce: "n-1", state: "st-1", codeChallenge: "desafio", codeChallengeMethod: "S256");
        var code = granted.Value!.Code;

        Assert.Equal("n-1", code.Nonce);
        Assert.Equal("st-1", code.State);
        Assert.Equal("desafio", code.CodeChallenge);
        Assert.Equal("S256", code.CodeChallengeMethod);
    }

    [Fact]
    public void ElCodigoExpiraConLaVigenciaDelCliente()
    {
        var granted = SignIn();

        Assert.Equal(TimeSpan.FromMinutes(5), granted.Value!.Code.ExpiresAt - granted.Value.Code.AuthenticatedAt);
    }

    [Fact]
    public void UnaContrasenaIncorrectaNoEmiteCodigo()
    {
        var granted = SignIn(password: "clave-incorrecta");

        Assert.False(granted.Succeeded);
        Assert.Equal("access_denied", granted.Error?.Code);
    }

    [Fact]
    public void UnUsuarioDesconocidoNoEmiteCodigo()
    {
        var granted = SignIn(userName: "nadie");

        Assert.False(granted.Succeeded);
        Assert.Equal("access_denied", granted.Error?.Code);
    }

    /// <summary>
    /// El error no distingue usuario de contrasena: hacerlo revelaria que cuentas existen.
    /// </summary>
    [Fact]
    public void UsuarioDesconocidoYContrasenaIncorrectaDanElMismoError()
    {
        Assert.Equal(
            SignIn(userName: "nadie", password: "otra").Error?.Description,
            SignIn(password: "clave-incorrecta").Error?.Description);
    }

    [Fact]
    public void DenegarDevuelveAccessDeniedYNoEmiteCodigo()
    {
        var denied = _service.Deny(ValidAuthorization());

        Assert.False(denied.Succeeded);
        Assert.Equal("access_denied", denied.Error?.Code);
    }

    private Core.Errors.Result<AuthorizationGranted> SignIn(
        string userName = UserName,
        string password = Password,
        IReadOnlyList<string>? scopes = null,
        string? nonce = null,
        string? state = null,
        string? codeChallenge = null,
        string? codeChallengeMethod = null) =>
        _service.SignIn(new SignInRequest(
            userName,
            password,
            ValidAuthorization(scopes, nonce, state, codeChallenge, codeChallengeMethod),
            ResponseModes.Query));

    private static ValidatedAuthorizationRequest ValidAuthorization(
        IReadOnlyList<string>? scopes = null,
        string? nonce = null,
        string? state = null,
        string? codeChallenge = null,
        string? codeChallengeMethod = null) =>
        new(
            ClientStoreFixture.Spa(),
            RedirectUri,
            scopes ?? ["openid"],
            ResponseTypeNames.Code,
            ResponseModes.Query,
            nonce,
            state,
            codeChallenge,
            codeChallengeMethod,
            Prompt: null);

    private static Dictionary<string, System.Text.Json.JsonElement> EmptyClaims() =>
        new Dictionary<string, System.Text.Json.JsonElement>();

    private sealed class SingleUserStore : IUserStore
    {
        public User? FindByUserName(string userName) =>
            string.Equals(userName, UserName, StringComparison.Ordinal)
                ? new User("user-1", UserName, Password, EmptyClaims())
                : null;

        public User? FindBySubject(string subject) =>
            string.Equals(subject, "user-1", StringComparison.Ordinal)
                ? new User("user-1", UserName, Password, EmptyClaims())
                : null;

        public IReadOnlyList<User> List() =>
            [new User("user-1", UserName, Password, EmptyClaims())];
    }
}