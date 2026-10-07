using OidcMock.Core.Authorization;
using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Core.Users;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Ruta de entrada /Account/Login del servidor real: muestra el login con el ReturnUrl oculto y,
/// tras autenticar al usuario, abre sesion y redirige a el. El ReturnUrl solo puede ser una ruta
/// local: aceptar una URL absoluta convertiria la ruta en un open redirect, porque su valor viene
/// de la barra de direcciones del navegador.
/// </summary>
public static class AccountLoginEndpoints
{
    private const string InvalidCredentials = "El usuario o la contrasena no son correctos.";
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

    private static IResult ShowLogin(HttpRequest request)
    {
        var returnUrl = ReturnUrl(request);

        return IsLocal(returnUrl)
            ? Results.Content(AccountLoginPage.Render(returnUrl!, error: null), HtmlContentType)
            : Results.Text(InvalidReturnUrl, statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> SignIn(
        HttpContext context,
        UserAuthenticator authenticator,
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

        var user = authenticator.Authenticate(ReadUserName(form), form.GetValueOrDefault(LoginFormFields.Password) ?? string.Empty);

        if (user is null)
        {
            return Results.Content(AccountLoginPage.Render(returnUrl!, InvalidCredentials), HtmlContentType);
        }

        var session = sessions.Start(user.UserName, user.Subject);

        AuthSessionCookie.Write(context.Response, session.SessionId, options, options.SessionLifetime);

        return Results.Redirect(returnUrl!);
    }

    private static string? ReturnUrl(HttpRequest request) => request.Query[AccountLoginPage.ReturnUrlField].FirstOrDefault();

    private static bool IsLocal(string? returnUrl) =>
        returnUrl is { Length: > 0 } value
            && value[0] == '/'
            && !value.StartsWith("//", StringComparison.Ordinal);

    /// <summary>
    /// Mismo criterio que la pantalla del authorize: se prefiere el campo tecleado "username" y se
    /// cae a "user", el de la lista desplegable.
    /// </summary>
    private static string ReadUserName(IReadOnlyDictionary<string, string> form) =>
        form.GetValueOrDefault(LoginFormFields.UserName) is { Length: > 0 } typed
            ? typed
            : form.GetValueOrDefault(LoginFormFields.User) ?? string.Empty;
}