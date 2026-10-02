using OidcMock.Core.Authorization;
using OidcMock.Core.Codes;
using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.PendingRequests;
using OidcMock.Core.Scopes;
using OidcMock.Core.Users;

namespace OidcMock.Core.DeviceAuthorization;

/// <summary>
/// Registra las peticiones de device authorization y de CIBA, y devuelve los handles con los que el
/// cliente sondea el token endpoint.
/// </summary>
/// <remarks>
/// El reloj entra inyectado y no se pide al sistema: la caducidad de la peticion pendiente es lo que
/// decide si el sondeo responde <c>expired_token</c> o <c>authorization_pending</c>, y eso tiene que
/// ser comprobable con un reloj de pruebas.
/// </remarks>
public sealed class PollAuthorizationService(
    IPendingAuthorizationStore pendingRequests,
    ClientAuthenticator clientAuthenticator,
    IUserStore userStore,
    TimeProvider timeProvider) : IPollAuthorizationService
{
    /// <summary>Intervalo de sondeo que sugiere RFC 8628 3.2 para el token endpoint.</summary>
    private const int DefaultIntervalInSeconds = 5;

    private static readonly TimeSpan DeviceCodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CibaLifetime = TimeSpan.FromMinutes(5);

    public Result<DeviceAuthorizationResponse> StartDevice(DeviceAuthorizationStart request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authenticated = AuthenticateClient(request.Credentials);
        if (authenticated.Failed)
        {
            return Failure<DeviceAuthorizationResponse>(authenticated.Error!);
        }

        var client = authenticated.Value!;
        var scopes = AllowedScopes(client, request.Scope);
        if (scopes is null)
        {
            return Failure<DeviceAuthorizationResponse>(ScopeNotAllowed());
        }

        var issued = timeProvider.GetUtcNow();
        var deviceCode = OpaqueToken.New();
        var userCode = UserCodeGenerator.New();

        pendingRequests.Issue(new PendingAuthorizationRequest(
            deviceCode,
            client.ClientId,
            scopes,
            AuthorizationWithoutRedirect(client, scopes),
            issued + DeviceCodeLifetime,
            TimeSpan.FromSeconds(DefaultIntervalInSeconds),
            deviceCode,
            userCode));

        return Result<DeviceAuthorizationResponse>.Ok(new DeviceAuthorizationResponse(
            deviceCode,
            userCode,
            VerificationUri.Constant,
            (int)DeviceCodeLifetime.TotalSeconds,
            DefaultIntervalInSeconds));
    }

    /// <summary>
    /// CIBA (OpenID Connect CIBA 1.0 7.1): el cliente inicia la autorizacion desde su backend y recibe
    /// un <c>auth_req_id</c> con el que sondea. El mock no pide confirmacion al usuario: si el
    /// <c>login_hint</c> identifica a alguien del store, la peticion queda aprobada de inmediato, que
    /// es el camino feliz para pruebas.
    /// </summary>
    public Result<CibaAuthorizationResponse> StartCiba(CibaAuthorizationStart request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authenticated = AuthenticateClient(request.Credentials);
        if (authenticated.Failed)
        {
            return Failure<CibaAuthorizationResponse>(authenticated.Error!);
        }

        var client = authenticated.Value!;

        // Un cliente sin login_hint no ha dicho a quien quiere autenticar: es invalid_request y no un
        // fallo de credenciales, porque el diagnostico falso hace que el cliente persiga el secreto.
        if (string.IsNullOrEmpty(request.LoginHint))
        {
            return Failure<CibaAuthorizationResponse>(ProtocolErrors.InvalidRequest(
                "El login_hint es obligatorio: sin el, el cliente no ha dicho a quien autenticar."));
        }

        var scopes = AllowedScopes(client, request.Scope);
        if (scopes is null)
        {
            return Failure<CibaAuthorizationResponse>(ScopeNotAllowed());
        }

        var issued = timeProvider.GetUtcNow();
        var handle = OpaqueToken.New();

        pendingRequests.Issue(new PendingAuthorizationRequest(
            handle,
            client.ClientId,
            scopes,
            AuthorizationWithoutRedirect(client, scopes),
            issued + CibaLifetime,
            TimeSpan.FromSeconds(DefaultIntervalInSeconds)));

        if (FindUser(request.LoginHint) is { } user)
        {
            pendingRequests.Approve(handle, user.UserName, user.Subject, issued);
        }

        return Result<CibaAuthorizationResponse>.Ok(new CibaAuthorizationResponse(
            handle,
            (int)CibaLifetime.TotalSeconds,
            DefaultIntervalInSeconds,
            request.WantsUserCode ? UserCodeGenerator.New() : null,
            request.WantsUserCode ? VerificationUri.Constant : null));
    }

    /// <summary>
    /// Autentica igual que el token endpoint: el secreto tiene que valer, y sin secreto solo pasan
    /// los clientes publicos.
    /// </summary>
    private Result<Client> AuthenticateClient(ClientCredentials credentials)
    {
        var client = clientAuthenticator.Authenticate(credentials);

        return client is null
            ? Result<Client>.Fail(ProtocolErrors.InvalidClient("Las credenciales del cliente no son validas."))
            : Result<Client>.Ok(client);
    }

    /// <summary>
    /// Sin <c>scope</c> se concede <c>openid</c>, que es lo minimo de una peticion de autenticacion.
    /// Con <c>scope</c>, todos los nombres tienen que estar permitidos para el cliente.
    /// </summary>
    private static string[]? AllowedScopes(Client client, string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? [ScopeNames.OpenId]
            : ScopeNames.Split(scope) is { } requested && requested.All(client.AllowsScope)
                ? requested
                : null;

    private User? FindUser(string loginHint) =>
        userStore.FindByUserName(loginHint) ?? userStore.FindBySubject(loginHint);

    /// <summary>
    /// Ni device authorization ni CIBA tienen <c>redirect_uri</c>: se construye una peticion con el
    /// cliente real y sin redireccion, para poder reutilizar el proyector de claims y la fabricacion
    /// de tokens.
    /// </summary>
    private static ValidatedAuthorizationRequest AuthorizationWithoutRedirect(
        Client client,
        IReadOnlyList<string> scopes) =>
        new(
            client,
            string.Empty,
            scopes,
            ResponseTypeNames.Code,
            ResponseModes.Query,
            Nonce: null,
            State: null,
            CodeChallenge: null,
            CodeChallengeMethod: null,
            Prompt: null);

    private static ProtocolError ScopeNotAllowed() =>
        ProtocolErrors.InvalidScope("Uno de los scopes solicitados no esta permitido para el cliente.");

    private static Result<T> Failure<T>(ProtocolError error) => Result<T>.Fail(error);
}

/// <summary>
/// User code corto y legible para que el usuario lo escriba en otro dispositivo.
/// </summary>
public static class UserCodeGenerator
{
    private const int UserCodeLength = 8;
    private const string Alphabet = "BCDFGHJKLMNPQRSTVWXZ";

    public static string New()
    {
        var random = System.Security.Cryptography.RandomNumberGenerator.GetBytes(UserCodeLength);

        return new string([.. random.Select(value => Alphabet[value % Alphabet.Length])]);
    }
}

/// <summary>
/// URI donde el usuario introduce el user code. El mock no necesita un PathBase propio: cuelga del
/// issuer que el cliente ya conoce.
/// </summary>
public static class VerificationUri
{
    public const string Constant = "urn:oidc-mock:device";
}