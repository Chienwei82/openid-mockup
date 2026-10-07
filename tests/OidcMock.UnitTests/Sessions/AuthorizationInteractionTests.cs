using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Authorization;
using OidcMock.Core.Configuration;
using OidcMock.Core.Grants;
using OidcMock.Core.Scopes;
using OidcMock.UnitTests.Fixtures;
using static OidcMock.UnitTests.Fixtures.AuthorizeValidation;

namespace OidcMock.UnitTests.Sessions;

/// <summary>
/// Matriz de prompt contra sesion, que es la regla que decide si el authorize redirige en
/// silencio, muestra login o muestra consentimiento.
/// </summary>
public sealed class AuthorizationInteractionTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryAuthSessionStore _sessions;
    private readonly InMemoryConsentStore _consents;
    private readonly AuthorizationInteraction _interaction;

    public AuthorizationInteractionTests()
    {
        _sessions = new InMemoryAuthSessionStore(_time, new OidcMockOptions());
        _consents = new InMemoryConsentStore();
        _interaction = new AuthorizationInteraction(_sessions, _consents);
    }

    [Fact]
    public void SinSesionPideLogin() =>
        Assert.Equal(AuthorizationStep.Login, Decide().Step);

    [Fact]
    public void SinSesionYConPromptNoneDevuelveLoginRequired()
    {
        var decision = Decide(prompt: PromptValues.None);

        Assert.Equal(AuthorizationStep.Error, decision.Step);
        Assert.Equal("login_required", decision.Error?.Code);
    }

    [Fact]
    public void ConSesionConcedeEnSilencio()
    {
        var decision = Decide(sessionId: OpenSession());

        Assert.Equal(AuthorizationStep.Grant, decision.Step);
        Assert.Equal("jperez", decision.UserName);
    }

    [Fact]
    public void ConSesionYPromptNoneTambienConcedeEnSilencio() =>
        Assert.Equal(AuthorizationStep.Grant, Decide(sessionId: OpenSession(), prompt: PromptValues.None).Step);

    [Fact]
    public void PromptLoginPideLoginAunqueHayaSesion() =>
        Assert.Equal(AuthorizationStep.Login, Decide(sessionId: OpenSession(), prompt: PromptValues.Login).Step);

    [Fact]
    public void PromptConsentPideConsentimientoAunqueHayaSesion() =>
        Assert.Equal(AuthorizationStep.Consent, Decide(sessionId: OpenSession(), prompt: PromptValues.Consent).Step);

    [Fact]
    public void PromptConsentSinSesionPideLoginYNoConsentimiento() =>
        Assert.Equal(AuthorizationStep.Login, Decide(prompt: PromptValues.Consent).Step);

    [Fact]
    public void UnaSesionCaducadaCuentaComoNoHaberSesion() =>
        Assert.Equal(AuthorizationStep.Login, Decide(sessionId: Caducada()).Step);

    [Fact]
    public void UnaSesionDesconocidaCuentaComoNoHaberSesion() =>
        Assert.Equal(AuthorizationStep.Login, Decide(sessionId: "sesion-inventada").Step);

    /// <summary>
    /// <c>select_account</c> pide elegir cuenta <b>siempre</b>, con o sin sesion: es lo que el prompt
    /// promete. La pantalla de identidad es la eleccion de cuenta (los perfiles son las cuentas), asi
    /// que el paso es un <see cref="AuthorizationStep.SelectAccount"/> propio y no un login. La
    /// decision lleva el usuario de la sesion para precargar su cuenta como la actual.
    /// </summary>
    [Fact]
    public void SelectAccountPideElegirCuentaAunqueHayaSesion() =>
        Assert.Equal(AuthorizationStep.SelectAccount, Decide(sessionId: OpenSession(), prompt: PromptValues.SelectAccount).Step);

    [Fact]
    public void SelectAccountSinSesionTambienPideElegirCuenta() =>
        Assert.Equal(AuthorizationStep.SelectAccount, Decide(prompt: PromptValues.SelectAccount).Step);

    [Fact]
    public void SelectAccountPrecargaAlUsuarioDeLaSesion() =>
        Assert.Equal("jperez", Decide(sessionId: OpenSession(), prompt: PromptValues.SelectAccount).UserName);

    /// <summary>
    /// El consentimiento es memorable: una vez aprobado para un cliente y un usuario, el mismo
    /// alcance no vuelve a preguntar aunque el prompt lo pida. Es el supuesto del mock (D-045): el
    /// flujo repetido del desarrollador no debe volver a hacer click.
    /// </summary>
    [Fact]
    public void PromptConsentConConsentimientoRecordadoConcedeEnSilencio()
    {
        _consents.Remember(ClientStoreFixture.SpaClientId, "jperez", [ScopeNames.OpenId]);

        Assert.Equal(
            AuthorizationStep.Grant,
            Decide(sessionId: OpenSession(), prompt: PromptValues.Consent).Step);
    }

    /// <summary>
    /// Lo recordado es un conjunto de scopes, no un visto bueno total: un scope nuevo vuelve a
    /// mostrar el consentimiento.
    /// </summary>
    [Fact]
    public void ElConsentimientoRecordadoNoCubreScopesAmpliados()
    {
        _consents.Remember(ClientStoreFixture.SpaClientId, "jperez", [ScopeNames.OpenId]);

        Assert.Equal(
            AuthorizationStep.Consent,
            Decide(
                sessionId: OpenSession(),
                prompt: PromptValues.Consent,
                scopes: [ScopeNames.OpenId, ScopeNames.Email]).Step);
    }

    private AuthorizationDecision Decide(string? sessionId = null, string? prompt = null, IReadOnlyList<string>? scopes = null) =>
        _interaction.Decide(AuthorizationWith(prompt, scopes), sessionId);

    private static ValidatedAuthorizationRequest AuthorizationWith(string? prompt, IReadOnlyList<string>? scopes = null) =>
        new(
            ClientStoreFixture.Spa(),
            AuthorizeValidation.RedirectUri,
            scopes ?? [ScopeNames.OpenId],
            ResponseTypeNames.Code,
            ResponseModes.Query,
            Nonce: null,
            State: null,
            CodeChallenge: null,
            CodeChallengeMethod: null,
            prompt);

    private string OpenSession() => _sessions.Start("jperez", "user-1").SessionId;

    private string Caducada()
    {
        var sessionId = OpenSession();
        _time.Advance(OidcMockOptions.DefaultSessionLifetime + TimeSpan.FromSeconds(1));

        return sessionId;
    }
}
