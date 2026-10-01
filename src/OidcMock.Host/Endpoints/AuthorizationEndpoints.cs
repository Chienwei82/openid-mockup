using OidcMock.Core.Authorization;
using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Endpoint /connect/authorize. GET valida la peticion y muestra la pantalla de login del mock; POST
/// procesa el formulario de esa pantalla y redirige al cliente con el codigo de autorizacion.
/// </summary>
public static class AuthorizationEndpoints
{
    public static IEndpointRouteBuilder MapAuthorizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<OidcMockOptions>();
        var group = endpoints.MapGroup(EndpointUri.NormalizePathBase(options.PathBase));

        group.MapGet(EndpointPaths.Authorize, ShowLoginPage);
        group.MapPost(EndpointPaths.Authorize, ProcessLogin);

        return endpoints;
    }

    private static async Task<IResult> ShowLoginPage(
        HttpContext context,
        IAuthorizationRequestValidator validator,
        Core.Users.IUserStore userStore,
        DiscoveryDocumentBuilder discoveryBuilder,
        OidcMockOptions options)
    {
        var request = await AuthorizationRequestBinder.BindAsync(context.Request);
        var validation = validator.Validate(request);

        if (validation.IsError)
        {
            return AuthorizationResponder.RespondToError(validation, IssuerResolver.Resolve(context.Request, discoveryBuilder, options));
        }

        return Results.Content(
            LoginPage.Render(validation.Value!, userStore.List(), request),
            "text/html; charset=utf-8");
    }

    private static async Task<IResult> ProcessLogin(
        HttpContext context,
        IAuthorizationRequestValidator validator,
        IAuthorizationService authorizationService,
        DiscoveryDocumentBuilder discoveryBuilder,
        OidcMockOptions options)
    {
        var request = await AuthorizationRequestBinder.BindAsync(context.Request);
        var validation = validator.Validate(request);
        var issuer = IssuerResolver.Resolve(context.Request, discoveryBuilder, options);

        if (validation.IsError)
        {
            return AuthorizationResponder.RespondToError(validation, issuer);
        }

        var authorized = await SignInAsync(context.Request, authorizationService, validation.Value!);

        return authorized.Succeeded
            ? AuthorizationResponder.RedirectWithCode(authorized.Value!, validation.Value!.ResponseMode, issuer)
            : AuthorizationResponder.RespondToError(
                AuthorizationValidationResult.Failed(authorized.Error!, validation.Value!.RedirectUri, validation.Value!.State),
                issuer);
    }

    /// <summary>
    /// Un POST sin formulario es una denegacion: el cliente volvio al endpoint sin_enviar credenciales.
    /// </summary>
    private static Task<Core.Errors.Result<AuthorizationGranted>> SignInAsync(
        HttpRequest request,
        IAuthorizationService authorizationService,
        ValidatedAuthorizationRequest authorization)
    {
        if (!request.HasFormContentType)
        {
            return Task.FromResult(authorizationService.Deny(authorization));
        }

        var form = RequestValues.ReadAsync(request);

        return ReadCredentials(request, authorizationService, authorization, form);
    }

    private static async Task<Core.Errors.Result<AuthorizationGranted>> ReadCredentials(
        HttpRequest request,
        IAuthorizationService authorizationService,
        ValidatedAuthorizationRequest authorization,
        Task<IReadOnlyDictionary<string, string>> pendingForm)
    {
        var form = await pendingForm;

        return authorizationService.SignIn(new SignInRequest(
            form.GetValueOrDefault("username") ?? string.Empty,
            form.GetValueOrDefault("password") ?? string.Empty,
            authorization,
            authorization.ResponseMode));
    }
}