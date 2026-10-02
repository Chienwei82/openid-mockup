using OidcMock.Core.Errors;

namespace OidcMock.Core.EndSession;

/// <summary>
/// Peticion de cierre de sesion (OpenID Connect RP-Initiated Logout 1). <c>SessionId</c> es el de la
/// cookie del navegador y <c>ClientId</c> el parametro que trae el cliente; si viene <c>IdTokenHint</c>,
/// el cliente se deduce de el y se ignoran ambos.
/// </summary>
public sealed record EndSessionRequest(
    string Issuer,
    string? IdTokenHint,
    string? PostLogoutRedirectUri,
    string? State,
    string? ClientId,
    string? SessionId);

/// <summary>
/// Que hay que hacer tras cerrar la sesion: a donde redirigir, si hay que redirigir, y a quien hay que
/// avisar por frontchannel logout. <c>RedirectUri</c> null significa que toca pagina de cierre.
/// </summary>
public sealed record EndSessionResult(
    string Issuer,
    string? RedirectUri,
    string? State,
    string? FrontchannelLogoutUri,
    string? SessionId);

/// <summary>
/// Cierra la sesion del navegador en /connect/endsession. Valida el <c>id_token_hint</c> y el
/// <c>post_logout_redirect_uri</c> contra el cliente registrado, porque ambos gobiernan una
/// redireccion fuera del mock.
/// </summary>
public interface IEndSessionService
{
    Result<EndSessionResult> EndSession(EndSessionRequest request);
}