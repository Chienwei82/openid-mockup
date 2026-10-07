using OidcMock.Core.Authorization;
using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Core.Users;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Ruta de entrada /Account/Login del servidor real: muestra la pantalla de identidad con el
/// ReturnUrl oculto y, tras aceptar, abre sesion y redirige a el. Denegar simula un fallo de
/// autenticacion y vuelve a mostrar la pantalla. El ReturnUrl solo puede ser una ruta local:
/// aceptar una URL absoluta convertiria la ruta en un open redirect, porque su valor viene de la
/// barra de direcciones del navegador.
/// </summary>
public static class AccountLoginEndpoints
{
    private const string DeniedAuthentication = "El mock simulo un fallo de autenticacion.";
    private const string InvalidReturnUrl = "El ReturnUrl solo puede ser una ruta local del servidor.";
    private const string HtmlContentType = "text/html; charset=utf-8";

    public static IEndpointRouteBuilder MapAccountLoginEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<OidcMockOptions>();
        var group = endpoints.MapGroup(EndpointUri.NormalizePathBase(options.PathBase));

        group.MapGet(EndpointPaths.AccountLogin, ShowLogin);
        group.MapPost(EndpointPaths.AccountLogin, SignIn);

        return endpoints;
    }

    private static IResult ShowLogin(HttpRequest request, IUserStore users)
    {
        var returnUrl = ReturnUrl(request);

        return IsLocal(returnUrl)
            ? ShowForm(returnUrl!, error: null, request.Query[LoginFormFields.Perfil].FirstOrDefault(), users)
            : Results.Text(InvalidReturnUrl, statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> SignIn(
        HttpContext context,
        IUserStore users,
        IUserOverlay overlay,
        IAuthSessionStore sessions,
        OidcMockOptions options)
    {
        if (!context.Request.HasFormContentType)
        {
            return Results.Text(InvalidReturnUrl, statusCode: StatusCodes.Status400BadRequest);
        }

        var form = await RequestValues.ReadAsync(context.Request);
        var returnUrl = form.GetValueOrDefault(AccountLoginPage.ReturnUrlField) ?? ReturnUrl(context.Request);

        if (!IsLocal(returnUrl))
        {
            return Results.Text(InvalidReturnUrl, statusCode: StatusCodes.Status400BadRequest);
        }

        if (IdentityForm.IsDenied(form))
        {
            return ShowForm(returnUrl!, DeniedAuthentication, form.GetValueOrDefault(LoginFormFields.Perfil), users);
        }

        var user = IdentityForm.ReadUser(form, users.List());
        overlay.Remember(user);

        var session = sessions.Start(user.UserName, user.Subject);

        AuthSessionCookie.Write(context.Response, session.SessionId, options, options.SessionLifetime);

        return Results.Redirect(returnUrl!);
    }

    private static IResult ShowForm(string returnUrl, string? error, string? perfil, IUserStore users)
    {
        var profiles = users.List();

        return Results.Content(
            AccountLoginPage.Render(returnUrl, error, IdentityForm.BaseProfile(perfil, profiles), profiles, PerfilHref(returnUrl)),
            HtmlContentType);
    }

    /// <summary>
    /// Los enlaces de perfil recargan esta misma pantalla con el ReturnUrl intacto: sin JavaScript,
    /// elegir perfil es una navegacion GET y los valores editados se pierden a proposito.
    /// </summary>
    private static Func<string, string> PerfilHref(string returnUrl) =>
        perfil => $"?{AccountLoginPage.ReturnUrlField}={Uri.EscapeDataString(returnUrl)}" +
            $"&{LoginFormFields.Perfil}={Uri.EscapeDataString(perfil)}";

    private static string? ReturnUrl(HttpRequest request) => request.Query[AccountLoginPage.ReturnUrlField].FirstOrDefault();

    private static bool IsLocal(string? returnUrl) =>
        returnUrl is { Length: > 0 } value
            && value[0] == '/'
            && !value.StartsWith("//", StringComparison.Ordinal);
}