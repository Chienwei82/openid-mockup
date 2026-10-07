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
    IConsentStore consents,
    IUserStore users,
    IUserOverlay overlay,
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
            AuthorizationStep.SelectAccount => Login(authorized, binding.Bound!, chooser: true, decision.UserName),
            AuthorizationStep.Consent => Consent(authorized, decision.UserName!),
            AuthorizationStep.Grant => Grant(authorized, decision.UserName!, SessionId()),
            _ => Login(authorized, binding.Bound!, chooser: false, sessionUserName: null)
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

    /// <summary>
    /// La pantalla de identidad cumple dos papeles: login y eleccion de cuenta (select_account). El
    /// perfil precargado es el pedido por el query, el de la sesion en la eleccion de cuenta, o el
    /// primero de users.json.
    /// </summary>
    private IResult Login(
        ValidatedAuthorizationRequest authorized,
        AuthorizationRequest request,
        bool chooser,
        string? sessionUserName)
    {
        var profiles = users.List();

        return Results.Content(
            LoginPage.Render(
                authorized,
                profiles,
                request,
                IdentityForm.BaseProfile(Perfil() ?? sessionUserName, profiles),
                PerfilHref(),
                chooser,
                IssuerResolver.PathBase(context.Request, options)),
            HtmlContentType);
    }

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
                ? ApproveConsent(authorized, session)
                : Denied(authorized);
    }

    /// <summary>
    /// La pantalla de consentimiento es la unica que deja constancia de la aprobacion: con ella se
    /// recuerda (D-045) y la proxima peticion igual no vuelve a preguntar.
    /// </summary>
    private IResult ApproveConsent(ValidatedAuthorizationRequest authorized, AuthSession session)
    {
        consents.Remember(authorized.Client.ClientId, session.UserName, authorized.Scopes);

        return Grant(authorized, session.UserName, session.SessionId);
    }

    /// <summary>
    /// El POST de la pantalla de identidad: Denegar simula un fallo de autenticacion y Aceptar
    /// concede con la identidad que el usuario edito. El mock no autentica a nadie; la identidad
    /// sintetica se recuerda en memoria para que el canje y userinfo devuelvan lo mismo.
    /// </summary>
    private IResult SignIn(ValidatedAuthorizationRequest authorized, IReadOnlyDictionary<string, string> form)
    {
        // Un POST sin formulario es una denegacion: el cliente volvio sin llegar a la pantalla.
        if (!context.Request.HasFormContentType || IdentityForm.IsDenied(form))
        {
            return Denied(authorized);
        }

        var user = IdentityForm.ReadUser(form, users.List());

        overlay.Remember(user);

        return RememberAndContinue(authorized, user.UserName);
    }

    /// <summary>
    /// Abre sesion para el usuario que acaba de autenticarse y sigue. Con prompt=consent el login
    /// solo autentica, asi que la eleccion del usuario va en la pantalla de consentimiento. La
    /// sesion recien abierta viaja como parametro porque su cookie se escribe en esta respuesta y
    /// el navegador no la devuelve hasta la peticion siguiente.
    /// </summary>
    private IResult RememberAndContinue(ValidatedAuthorizationRequest authorized, string userName)
    {
        var session = OpenSession(userName);

        AuthSessionCookie.Write(context.Response, session.SessionId, IssuerResolver.PathBase(context.Request, options), options.SessionLifetime);

        return authorized.Prompt == PromptValues.Consent
            ? Consent(authorized, session.UserName)
            : Grant(authorized, session.UserName, session.SessionId);
    }

    private IResult Grant(ValidatedAuthorizationRequest authorized, string userName, string? sessionId)
    {
        var granted = authorization.Approve(new AuthorizationApproval(userName, authorized, sessionId));

        if (granted.Succeeded)
        {
            ConsumeRequestUri(authorized);
        }

        return granted.Succeeded
            ? RespondGranted(granted.Value!, authorized, userName, sessionId)
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
        string userName,
        string? sessionId) =>
        authorized.ResponseType == ResponseTypeNames.CodeIdToken
            ? RespondHybrid(granted, authorized, userName, sessionId)
            : AuthorizationResponder.RedirectWithCode(granted, authorized.ResponseMode, Issuer);

    private IResult RespondHybrid(
        AuthorizationGranted granted,
        ValidatedAuthorizationRequest authorized,
        string userName,
        string? sessionId)
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
            AuthorizationCode: granted.Code.Code,
            SessionId: sessionId));

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

    private string? Perfil() => context.Request.Query[LoginFormFields.Perfil].FirstOrDefault();

    /// <summary>
    /// Los enlaces de perfil repiten la peticion de autorizacion actual con otro perfil: sin
    /// JavaScript, elegir perfil es una navegacion GET y los valores editados se pierden a proposito.
    /// </summary>
    private Func<string, string> PerfilHref()
    {
        var query = string.Join(
            '&',
            context.Request.Query
                .Where(parameter => parameter.Key != LoginFormFields.Perfil)
                .Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value.ToString())}"));

        return perfil => $"?{query}&{LoginFormFields.Perfil}={Uri.EscapeDataString(perfil)}";
    }

    private static bool IsAllowed(string decision) =>
        string.Equals(decision, ConsentPage.Allow, StringComparison.Ordinal);

    private const string HtmlContentType = "text/html; charset=utf-8";
}
