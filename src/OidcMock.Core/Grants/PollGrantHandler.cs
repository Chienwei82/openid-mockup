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

    public Result<TokenResponse> Handle(TokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var found = pendingRequests.Find(HandleFrom(request));
        if (found.Failed)
        {
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidGrant(
                "El codigo de autorizacion es invalido o ya caduco."));
        }

        var pending = found.Value!;
        var pollingError = PollError(pending);
        if (pollingError is not null)
        {
            return Result<TokenResponse>.Fail(pollingError);
        }

        if (!string.Equals(pending.ClientId, request.Client.ClientId, StringComparison.Ordinal))
        {
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidGrant(
                "El codigo de autorizacion no fue emitido para este cliente."));
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
            includeIdToken: IssuesIdToken,
            includeRefreshToken: true);
    }

    /// <summary>
    /// Respuesta al sondeo antes de que el usuario decida. Una peticion denegada se marca dejando el
    /// subject vacio y el prompt en "deny", que es lo que el usuario eligio en la pantalla.
    /// </summary>
    private static ProtocolError? PollError(PendingAuthorizationRequest pending) =>
        pending.Subject is not null
            ? null
            : pending.Denied
                ? ProtocolErrors.AccessDenied("El usuario denego la solicitud.")
                : ProtocolErrors.AuthorizationPending("El usuario aun no ha aprobado la solicitud.");
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
