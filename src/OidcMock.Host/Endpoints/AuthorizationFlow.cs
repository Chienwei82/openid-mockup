using OidcMock.Core.Authorization;
using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Core.Errors;
using OidcMock.Core.PushedRequests;
using OidcMock.Core.Users;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Orquestacion de una peticion a /connect/authorize. Se crea por peticion y lleva las
/// dependencias del caso de uso, de modo que los metodos son cortos y cada uno hace un paso:
/// validar, decidir la pantalla,Responder. La sesion del navegador se lee de la cookie propia.
/// </summary>
internal sealed class AuthorizationFlow(
    HttpContext context,
    IAuthorizationRequestValidator validator,
    IAuthorizationService authorization,
    IAuthorizationInteraction interaction,
    IUserStore users,
    IAuthSessionStore sessions,
    IPushedAuthorizationService pushedRequests,
    DiscoveryDocumentBuilder discovery,
    OidcMockOptions options)
{
    private const string ExpiredSessionDescription = "La sesion del navegador caduco antes de consenting.";

    public async Task<IResult> ShowAsync()
    {
        var request = await BindAsync();
        if (request.Failed)
        {
            return RespondToError(
                AuthorizationValidationResult.Failed(request.Error!, null, null),
                ResponseModes.Query);
        }

        var bound = request.Value!;
        var validation = validator.Validate(bound);

        if (validation.IsError)
        {
            return RespondToError(validation, bound.ResponseMode);
        }

        var authorized = validation.Value!;
        var decision = interaction.Decide(authorized, SessionId());

        return decision.Step switch
        {
            AuthorizationStep.Error => RespondToError(
                AuthorizationValidationResult.Failed(decision.Error!, authorized.RedirectUri, authorized.State),
                authorized.ResponseMode),
            AuthorizationStep.Consent => Consent(authorized, decision.UserName!),
            AuthorizationStep.Grant => Grant(authorized, decision.UserName!),
            _ => Login(authorized, bound)
        };
    }

    public async Task<IResult> ProcessAsync()
    {
        var request = await BindAsync();
        if (request.Failed)
        {
            return RespondToError(
                AuthorizationValidationResult.Failed(request.Error!, null, null),
                ResponseModes.Query);
        }

        var bound = request.Value!;
        var validation = validator.Validate(bound);

        if (validation.IsError)
        {
            return RespondToError(validation, bound.ResponseMode);
        }

        var form = await RequestValues.ReadAsync(context.Request);

        return form.ContainsKey(ConsentPage.DecisionField)
            ? AnswerConsent(validation.Value!, form)
            : SignIn(validation.Value!, form);
    }

    /// <summary>
    /// Enlaza la peticion y, si vino empujada (RFC 9126), la sustituye por la original. Un cliente
    /// que anuncia PAR llega al authorize con solo <c>client_id</c> y <c>request_uri</c>: sin esto,
    /// el authorize no veria <c>redirect_uri</c> ni <c>response_type</c> y rechazaria a un cliente
    /// bien configurado.
    /// </summary>
    private async Task<Result<AuthorizationRequest>> BindAsync()
    {
        var request = await AuthorizationRequestBinder.BindAsync(context.Request);

        if (string.IsNullOrEmpty(request.RequestUri))
        {
            return Result<AuthorizationRequest>.Ok(request);
        }

        var resolved = pushedRequests.Find(request.RequestUri);

        // Un request_uri desconocido o caducado es invalid_request_uri (RFC 9126 4.1), no un invalid_request
        // generico: el cliente puede distinguirlo y reintentar empujando la peticion otra vez.
        return resolved.Failed
            ? Result<AuthorizationRequest>.Fail(AuthorizationErrors.InvalidRequestUri(resolved.Error!.Description))
            : resolved;
    }

    private IResult Login(ValidatedAuthorizationRequest authorized, AuthorizationRequest request) =>
        Results.Content(LoginPage.Render(authorized, users.List(), request), HtmlContentType);

    private static IResult Consent(ValidatedAuthorizationRequest authorized, string userName) =>
        Results.Content(ConsentPage.Render(authorized, userName), HtmlContentType);

    /// <summary>
    /// El usuario ya estaba autenticado y solo decide. Sin sesion no se concede nada: la pantalla
    /// de consentimiento no autentica a nadie, asi que el cliente tiene que empezar el flujo otra vez.
    /// </summary>
    private IResult AnswerConsent(ValidatedAuthorizationRequest authorized, IReadOnlyDictionary<string, string> form)
    {
        var session = Session();

        return session is null
            ? RespondToError(
                AuthorizationValidationResult.Failed(
                    AuthorizationErrors.LoginRequired(ExpiredSessionDescription),
                    authorized.RedirectUri,
                    authorized.State),
                authorized.ResponseMode)
            : IsAllowed(form[ConsentPage.DecisionField])
                ? Grant(authorized, session.UserName)
                : Denied(authorized);
    }

    private IResult SignIn(ValidatedAuthorizationRequest authorized, IReadOnlyDictionary<string, string> form)
    {
        // Un POST sin formulario es una denegacion: el cliente volvio sin llegar a enviar credenciales.
        if (!context.Request.HasFormContentType)
        {
            return Denied(authorized);
        }

        var signedIn = authorization.SignIn(new SignInRequest(
            ReadUserName(form),
            form.GetValueOrDefault(LoginFormFields.Password) ?? string.Empty,
            authorized,
            authorized.ResponseMode));

        return signedIn.Failed
            ? RespondToError(
                AuthorizationValidationResult.Failed(
                    signedIn.Error!,
                    authorized.RedirectUri,
                    authorized.State),
                authorized.ResponseMode)
            : RememberAndContinue(authorized, signedIn.Value!.Code.UserName);
    }

    /// <summary>
    /// Abre sesion para el usuario que acaba de autenticarse y sigue. Con prompt=consent el login
    /// solo autentica, asi que la eleccion del usuario va en la pantalla de consentimiento.
    /// </summary>
    private IResult RememberAndContinue(ValidatedAuthorizationRequest authorized, string userName)
    {
        var session = OpenSession(userName);

        AuthSessionCookie.Write(context.Response, session.SessionId, options, options.SessionLifetime);

        return authorized.Prompt == PromptValues.Consent
            ? Consent(authorized, session.UserName)
            : Grant(authorized, session.UserName);
    }

    private IResult Grant(ValidatedAuthorizationRequest authorized, string userName)
    {
        var granted = authorization.Approve(new AuthorizationApproval(userName, authorized));

        if (granted.Succeeded)
        {
            ConsumeRequestUri(authorized);
        }

        return granted.Succeeded
            ? AuthorizationResponder.RedirectWithCode(granted.Value!, authorized.ResponseMode, Issuer)
            : RespondToError(
                AuthorizationValidationResult.Failed(
                    granted.Error!,
                    authorized.RedirectUri,
                    authorized.State),
                authorized.ResponseMode);
    }

    /// <summary>
    /// Invalida el request_uri cuando la autorizacion se concede. RFC 9126 4 lo declara de un solo uso:
    /// si siguiera vivo, la misma peticion empujada podria emitir un segundo codigo.
    /// </summary>
    private void ConsumeRequestUri(ValidatedAuthorizationRequest authorized)
    {
        if (!string.IsNullOrEmpty(authorized.RequestUri))
        {
            pushedRequests.Consume(authorized.RequestUri);
        }
    }

    private IResult Denied(ValidatedAuthorizationRequest authorized) =>
        RespondToError(
            AuthorizationValidationResult.Failed(
                AuthorizationErrors.AccessDenied("El usuario denego la autorizacion."),
                authorized.RedirectUri,
                authorized.State),
            authorized.ResponseMode);

    private IResult RespondToError(AuthorizationValidationResult validation, string responseMode) =>
        AuthorizationResponder.RespondToError(validation, Issuer, responseMode);

    private AuthSession OpenSession(string userName) =>
        sessions.Start(userName, users.FindByUserName(userName)?.Subject ?? userName);

    private AuthSession? Session() => SessionId() is { } id ? sessions.Find(id) : null;

    private string? SessionId() => AuthSessionCookie.Read(context.Request);

    private string Issuer => IssuerResolver.Resolve(context.Request, discovery, options);

    /// <summary>
    /// El campo de la lista desplegable es "user" y el de texto libre "username"; se prefiere el
    /// tecleado, porque es el explicito, y se cae al otro para no romper formularios anteriores.
    /// </summary>
    private static string ReadUserName(IReadOnlyDictionary<string, string> form) =>
        form.GetValueOrDefault(LoginFormFields.UserName) is { Length: > 0 } typed
            ? typed
            : form.GetValueOrDefault(LoginFormFields.User) ?? string.Empty;

    private static bool IsAllowed(string decision) =>
        string.Equals(decision, ConsentPage.Allow, StringComparison.Ordinal);

    private const string HtmlContentType = "text/html; charset=utf-8";
}
