using OidcMock.Core.Errors;
using OidcMock.Core.PendingRequests;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;

namespace OidcMock.Core.Grants;

/// <summary>
/// Base de los grants que el cliente sondea con un handle en vez de aportar un secreto: device_code
/// y CIBA. Ambos comparten el mismo ciclo de RFC 8628: mientras el usuario no responde se devuelve
/// authorization_pending, si lo denego access_denied, y si expiro expired_token.
/// </summary>
public abstract class PollGrantHandler(
    IPendingAuthorizationStore pendingRequests,
    IUserStore userStore,
    IRefreshTokenStore refreshTokenStore,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider)
{
    /// <summary>Handle que viaja en la peticion: device_code o auth_req_id, segun el grant.</summary>
    protected abstract string HandleFrom(TokenRequest request);

    public virtual bool IssuesIdToken => true;

    public Task<Result<TokenResponse>> HandleAsync(TokenRequest request) =>
        Task.FromResult(Handle(request));

    private Result<TokenResponse> Handle(TokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var found = pendingRequests.Poll(HandleFrom(request));
        if (found.Failed)
        {
            // El handle de un flujo por sondeo es un device_code o un auth_req_id, nunca un codigo de
            // autorizacion: el mensaje forma parte de la superficie observable del mock y el cliente
            // lo muestra, asi que tiene que nombrar lo que el cliente realmente presento.
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidGrant(
                "El handle es invalido o ya fue canjeado."));
        }

        var pending = found.Value!;
        var lifecycleError = LifecycleError(pending);
        if (lifecycleError is not null)
        {
            return Result<TokenResponse>.Fail(lifecycleError);
        }

        if (!string.Equals(pending.ClientId, request.Client.ClientId, StringComparison.Ordinal))
        {
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidGrant(
                "El handle no fue emitido para este cliente."));
        }

        var user = userStore.FindBySubject(pending.Subject!);
        if (user is null)
        {
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidGrant(
                "El usuario que aprobo la solicitud ya no existe en el mock."));
        }

        var redeemed = pendingRequests.Redeem(pending.Handle);
        if (redeemed.Failed)
        {
            return Result<TokenResponse>.Fail(redeemed.Error!);
        }

        return TokenResponseFactory.Issue(
            tokenFactory,
            refreshTokenStore,
            timeProvider,
            request.Issuer,
            request.Client,
            user,
            pending.Scopes,
            pending.AuthenticatedAt ?? timeProvider.GetUtcNow(),
            null,
            null,
            includeIdToken: IssuesIdToken && IdTokenRules.GrantsIdToken(pending.Scopes),
            includeRefreshToken: true);
    }

    /// <summary>
    /// Los tres finales del ciclo de sondeo, en el orden en que se comprueban (RFC 8628 3.5). Cada uno
    /// dice algo distinto al cliente: <c>expired_token</c> que pida un handle nuevo, <c>slow_down</c> que
    /// espere el intervalo anunciado, y los dos ultimos si el usuario todavia no ha decidido.
    /// </summary>
    private ProtocolError? LifecycleError(PendingAuthorizationRequest pending)
    {
        var now = timeProvider.GetUtcNow();

        if (pending.IsExpiredAt(now))
        {
            return ProtocolErrors.ExpiredToken("El handle caduco antes de que el usuario respondiera.");
        }

        // El intervalo se mira despues de la caducidad: un handle caducado ya no tiene a quien esperar.
        if (pending.PolledTooSoonAt(now))
        {
            return ProtocolErrors.SlowDown("El cliente esta sondeando mas rapido que el intervalo anunciado.");
        }

        return pending.Subject is not null
            ? null
            : pending.Denied
                ? ProtocolErrors.AccessDenied("El usuario denego la solicitud.")
                : ProtocolErrors.AuthorizationPending("El usuario aun no ha aprobado la solicitud.");
    }
}

/// <summary>
/// Grant device_code (RFC 8628 3.4): el dispositivo sondea el token endpoint con el device_code.
/// </summary>
public sealed class DeviceCodeGrantHandler(
    IPendingAuthorizationStore pendingRequests,
    IUserStore userStore,
    IRefreshTokenStore refreshTokenStore,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider)
    : PollGrantHandler(pendingRequests, userStore, refreshTokenStore, tokenFactory, timeProvider), IGrantHandler
{
    public string GrantType => GrantTypes.DeviceCode;

    protected override string HandleFrom(TokenRequest request) => request.DeviceCode ?? string.Empty;
}

/// <summary>
/// Grant de CIBA (Client Initiated Backchannel Authentication): el cliente sondea con el auth_req_id
/// que le devolvio /connect/ciba.
/// </summary>
public sealed class CibaGrantHandler(
    IPendingAuthorizationStore pendingRequests,
    IUserStore userStore,
    IRefreshTokenStore refreshTokenStore,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider)
    : PollGrantHandler(pendingRequests, userStore, refreshTokenStore, tokenFactory, timeProvider), IGrantHandler
{
    public string GrantType => GrantTypes.Ciba;

    protected override string HandleFrom(TokenRequest request) => request.DeviceCode ?? string.Empty;
}
