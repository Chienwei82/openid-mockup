using OidcMock.Core.Errors;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Aplica las reglas de prompt de OpenID Connect Core 3.1.2.1 sobre la sesion del navegador.
/// Ningun prompt exige interaccion si no hay sesion: en ese caso, o se pide login o se devuelve
/// login_required, segun el prompt venga o no.
/// </summary>
public sealed class AuthorizationInteraction(IAuthSessionStore sessions) : IAuthorizationInteraction
{
    private const string LoginRequiredDescription =
        "El prompt=none exige una sesion activa y el navegador no trae ninguna.";

    public AuthorizationDecision Decide(ValidatedAuthorizationRequest authorization, string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(sessions);

        var session = string.IsNullOrEmpty(sessionId) ? null : sessions.Find(sessionId);
        var silent = authorization.Prompt == PromptValues.None;

        // prompt=none prohibe cualquier pantalla, asi que sin sesion solo cabe un error.
        if (silent)
        {
            return session is null
                ? AuthorizationDecision.Fails(AuthorizationErrors.LoginRequired(LoginRequiredDescription))
                : AuthorizationDecision.Grants(session.UserName);
        }

        // prompt=select_account pide elegir cuenta siempre, con o sin sesion: es lo que el prompt
        // promete (D-043). La pantalla de identidad es la eleccion de cuenta, y la decision lleva el
        // usuario de la sesion para precargar su cuenta como la actual.
        if (authorization.Prompt == PromptValues.SelectAccount)
        {
            return AuthorizationDecision.NeedsAccountSelection(session?.UserName);
        }

        // prompt=login invalida la sesion previa, por eso va antes de mirar si existe.
        if (authorization.Prompt == PromptValues.Login || session is null)
        {
            return AuthorizationDecision.NeedsLogin();
        }

        return authorization.Prompt == PromptValues.Consent
            ? AuthorizationDecision.NeedsConsent(session.UserName)
            : AuthorizationDecision.Grants(session.UserName);
    }
}
