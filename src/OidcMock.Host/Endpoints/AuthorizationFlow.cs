using OidcMock.Core.Authorization;
using OidcMock.Core.Configuration;
using OidcMock.Core.Codes;
using OidcMock.Core.Discovery;
using OidcMock.Core.Errors;
using OidcMock.Core.PushedRequests;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Orquestacion de una peticion a /connect/authorize. Se crea por peticion y lleva las
/// dependencias del caso de uso, de modo que los metodos son cortos y cada uno hace un paso:
/// validar, decidir la pantalla,Responder. La sesion del navegador se lee de la cookie propia.
/// </summary>
internal sealed class AuthorizationFlow(
    HttpContext context,
    AuthorizationBinder binder,
    IAuthorizationService authorization,
    IAuthorizationInteraction interaction,
    IUserStore users,
    IAuthSessionStore sessions,
    ITokenFactory tokens,
    DiscoveryDocumentBuilder discovery,
    OidcMockOptions options)
{
    private const string ExpiredSessionDescription = "La sesion del navegador caduco antes de consenting.";
    private const string MissingUserDescription = "El usuario de la sesion ya no existe en el mock.";

    public async Task<IResult> ShowAsync()
    {
        var binding = await binder.Bind(context.Request);

        if (binding.Authorized is not { } authorized)
        {
            return RespondBindingError(binding);
        }

        var decision = interaction.Decide(authorized, SessionId());

        return decision.Step switch
        {
            AuthorizationStep.Error => RespondToError(
                AuthorizationValidationResult.Failed(decision.Error!, authorized.RedirectUri, authorized.State),
                authorized.ResponseMode),
            AuthorizationStep.Consent => Consent(authorized, decision.UserName!),
            AuthorizationStep.Grant => Grant(authorized, decision.UserName!),
            _ => Login(authorized, binding.Bound!)
        };
    }

    public async Task<IResult> ProcessAsync()
    {
        var binding = await binder.Bind(context.Request);

        if (binding.Authorized is not { } authorized)
        {
            return RespondBindingError(binding);
        }

        var form = await RequestValues.ReadAsync(context.Request);

        return IsConsentAnswer(form)
            ? AnswerConsent(authorized, form)
            : SignIn(authorized, form);
    }

    /// <summary>
    /// Distingue el POST de consentimiento del POST de login por el campo que trae el formulario. La
    /// regla vive aqui, con nombre, y no repartida en el <c>if</c> que elige la pantalla.
    /// </summary>
    private static bool IsConsentAnswer(IReadOnlyDictionary<string, string> form) =>
        form.ContainsKey(ConsentPage.DecisionField);

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
            ? RespondGranted(granted.Value!, authorized, userName)
            : RespondToError(
                AuthorizationValidationResult.Failed(
                    granted.Error!,
                    authorized.RedirectUri,
                    authorized.State),
                authorized.ResponseMode);
    }

    /// <summary>
    /// Elige la respuesta segun el response_type: el flujo de codigo devuelve solo el code, y el
    /// hibrido emite ademas el id_token (con c_hash del code) y el session_state, como el servidor
    /// real al que sustituye este mock.
    /// </summary>
    private IResult RespondGranted(
        AuthorizationGranted granted,
        ValidatedAuthorizationRequest authorized,
        string userName) =>
        authorized.ResponseType == ResponseTypeNames.CodeIdToken
            ? RespondHybrid(granted, authorized, userName)
            : AuthorizationResponder.RedirectWithCode(granted, authorized.ResponseMode, Issuer);

    private IResult RespondHybrid(
        AuthorizationGranted granted,
        ValidatedAuthorizationRequest authorized,
        string userName)
    {
        var user = users.FindByUserName(userName);

        if (user is null)
        {
            return RespondToError(
                AuthorizationValidationResult.Failed(
                    AuthorizationErrors.AccessDenied(MissingUserDescription),
                    authorized.RedirectUri,
                    authorized.State),
                authorized.ResponseMode);
        }

        var idToken = tokens.CreateIdToken(new IdTokenRequest(
            Issuer,
            authorized.Client.ClientId,
            authorized.Scopes,
            user,
            granted.Code.AuthenticatedAt,
            authorized.Client.TokenLifetimes.IdentityToken,
            authorized.Nonce,
            AuthorizationCode: granted.Code.Code));

        return AuthorizationResponder.RedirectWithHybrid(
            granted,
            idToken,
            SessionStateValue.New(),
            authorized.ResponseMode,
            Issuer);
    }

    /// <summary>
    /// Invalida el request_uri cuando la autorizacion se concede. RFC 9126 4 lo declara de un solo uso:
    /// si siguiera vivo, la misma peticion empujada podria emitir un segundo codigo.
    /// </summary>
    private void ConsumeRequestUri(ValidatedAuthorizationRequest authorized)
    {
        if (!string.IsNullOrEmpty(authorized.RequestUri))
        {
            binder.ConsumeRequestUri(authorized.RequestUri);
        }
    }

    private IResult Denied(ValidatedAuthorizationRequest authorized) =>
        RespondToError(
            AuthorizationValidationResult.Failed(
                AuthorizationErrors.AccessDenied("El usuario denego la autorizacion."),
                authorized.RedirectUri,
                authorized.State),
            authorized.ResponseMode);

    /// <summary>
    /// Traduce un enlace fallido a la respuesta que corresponde. Cuando la peticion llego a enlazarse,
    /// el error va al <c>redirect_uri</c> del cliente (RFC 6749 4.1.2.1): el navegador del usuario tiene
    /// que ver el fallo en la aplicacion a la que queria entrar. Solo si ni siquiera se pudo enlazar
    /// (un <c>request_uri</c> desconocido, una peticion ilegible) se responde aqui, porque no hay a
    /// quien redirigir y la unica forma de que el cliente se entere es ver el error de frente.
    /// </summary>
    private IResult RespondBindingError(AuthorizationBinder.Binding binding)
    {
        var bound = binding.Bound;

        // El response mode se decide por defecto en el query: si la peticion no llego a leerse, no hay
        // modo declarado al que ajustarse.
        return RespondToError(binding.Validation, bound?.ResponseMode ?? ResponseModes.Query);
    }

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
