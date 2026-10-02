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
    private readonly AuthorizationInteraction _interaction;

    public AuthorizationInteractionTests()
    {
        _sessions = new InMemoryAuthSessionStore(_time, new OidcMockOptions());
        _interaction = new AuthorizationInteraction(_sessions);
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

    [Fact]
    public void SelectAccountSeTrataComoSinPrompt() =>
        Assert.Equal(AuthorizationStep.Grant, Decide(sessionId: OpenSession(), prompt: PromptValues.SelectAccount).Step);

    private AuthorizationDecision Decide(string? sessionId = null, string? prompt = null) =>
        _interaction.Decide(AuthorizationWith(prompt), sessionId);

    private static ValidatedAuthorizationRequest AuthorizationWith(string? prompt) =>
        new(
            ClientStoreFixture.Spa(),
            AuthorizeValidation.RedirectUri,
            [ScopeNames.OpenId],
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
