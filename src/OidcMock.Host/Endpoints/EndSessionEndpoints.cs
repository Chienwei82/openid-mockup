using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Core.EndSession;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// GET y POST /connect/endsession (OpenID Connect RP-Initiated Logout 1). Acepta id_token_hint,
/// post_logout_redirect_uri, state y client_id en query o en formulario, cierra la sesion del
/// navegador y redirige al cliente si el redirect esta registrado; si no, muestra la pagina de cierre,
/// que es donde se avisa por frontchannel logout.
/// </summary>
public static class EndSessionEndpoints
{
    private const string IdTokenHintField = "id_token_hint";
    private const string PostLogoutRedirectUriField = "post_logout_redirect_uri";
    private const string StateField = "state";
    private const string ClientIdField = "client_id";
    private const string HtmlContentType = "text/html; charset=utf-8";

    public static IEndpointRouteBuilder MapEndSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<OidcMockOptions>();
        var group = endpoints.MapGroup(EndpointUri.NormalizePathBase(options.PathBase));

        group.MapGet(EndpointPaths.EndSession, EndSession);
        group.MapPost(EndpointPaths.EndSession, EndSession);

        return endpoints;
    }

    /// <summary>
    /// La cookie se borra siempre, incluso cuando la peticion es invalida: el navegador ya no debe
    /// llevar una sesion que el mock ha rechazado cerrar, y dejarla puesta haria que el siguiente
    /// authorize siguiera concediendo codigos en silencio.
    /// </summary>
    private static async Task<IResult> EndSession(
        HttpContext context,
        IEndSessionService endSession,
        DiscoveryDocumentBuilder discoveryBuilder,
        OidcMockOptions options)
    {
        var values = await RequestValues.ReadAsync(context.Request);

        AuthSessionCookie.Clear(context.Response);

        var result = endSession.EndSession(new EndSessionRequest(
            IssuerResolver.Resolve(context.Request, discoveryBuilder, options),
            values.GetValueOrDefault(IdTokenHintField),
            values.GetValueOrDefault(PostLogoutRedirectUriField),
            values.GetValueOrDefault(StateField),
            values.GetValueOrDefault(ClientIdField),
            AuthSessionCookie.Read(context.Request)));

        return result.Failed ? ProtocolErrorResults.From(result.Error!) : Respond(context, result.Value!);
    }

    private static IResult Respond(HttpContext context, EndSessionResult result) =>
        result.RedirectUri is null
            ? Results.Content(EndSessionPage.Render(result), HtmlContentType)
            : Results.Redirect(WithState(result.RedirectUri, result.State));

    private static string WithState(string redirectUri, string? state) =>
        string.IsNullOrEmpty(state)
            ? redirectUri
            : QueryStringHelper.AppendQuery(redirectUri, new Dictionary<string, string> { ["state"] = state });
}