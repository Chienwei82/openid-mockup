using System.Text.Json.Serialization;
using OidcMock.Core.Authorization;
using OidcMock.Core.Clients;
using OidcMock.Core.Codes;
using OidcMock.Core.Configuration;
using OidcMock.Core.DeviceAuthorization;
using OidcMock.Core.Discovery;
using OidcMock.Core.Errors;
using OidcMock.Core.PendingRequests;
using OidcMock.Core.Users;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Endpoints de autorizacion por sondeo: device authorization, CIBA y el iframe de check_session.
/// </summary>
public static class PollEndpoints
{
    private static readonly TimeSpan CibaLifetime = TimeSpan.FromMinutes(5);
    private const int CibaIntervalInSeconds = 5;

    public static IEndpointRouteBuilder MapPollEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<OidcMockOptions>();
        var group = endpoints.MapGroup(EndpointUri.NormalizePathBase(options.PathBase));

        group.MapPost(EndpointPaths.DeviceAuthorization, StartDeviceAuthorization);
        group.MapPost(EndpointPaths.Ciba, StartCiba);
        group.MapGet(
            EndpointPaths.CheckSession,
            () => Results.Content(CheckSessionPage.Body, "text/html; charset=utf-8"));

        return endpoints;
    }

    private static async Task<IResult> StartDeviceAuthorization(
        HttpContext context,
        IDeviceAuthorizationService deviceAuthorization,
        IClientStore clientStore)
    {
        var values = await RequestValues.ReadAsync(context.Request);
        var client = clientStore.Find(values.GetValueOrDefault("client_id") ?? string.Empty);
        if (client is null)
        {
            return UnknownClient();
        }

        var scopes = Scopes(values.GetValueOrDefault("scope"));
        if (!scopes.All(client.AllowsScope))
        {
            return ProtocolErrorResults.From(ProtocolErrors.InvalidScope(
                "Uno de los scopes solicitados no esta permitido para el cliente."));
        }

        var result = deviceAuthorization.Start(new DeviceAuthorizationRequest(client, scopes));

        return result.Failed
            ? ProtocolErrorResults.From(result.Error!)
            : Results.Json(new DeviceAuthorizationBody(
                result.Value!.DeviceCode,
                result.Value.UserCode,
                result.Value.VerificationUri,
                result.Value.ExpiresIn,
                result.Value.Interval));
    }

    /// <summary>
    /// CIBA: el cliente inicia la autorizacion desde su backend y recibe un auth_req_id con el que
    /// sondea. El mock no pide confirmacion al usuario: si el login_hint identifica a un usuario del
    /// store, la peticion queda aprobada de inmediato, que es el camino feliz para pruebas.
    /// </summary>
    private static async Task<IResult> StartCiba(
        HttpContext context,
        IPendingAuthorizationStore pendingRequests,
        IClientStore clientStore,
        IUserStore userStore,
        DiscoveryDocumentBuilder discoveryBuilder,
        OidcMockOptions options)
    {
        var values = await RequestValues.ReadAsync(context.Request);
        var client = clientStore.Find(values.GetValueOrDefault("client_id") ?? string.Empty);
        var loginHint = values.GetValueOrDefault("login_hint");

        if (client is null || string.IsNullOrEmpty(loginHint))
        {
            return UnknownClient();
        }

        var issuer = IssuerResolver.Resolve(context.Request, discoveryBuilder, options);
        var scopes = Scopes(values.GetValueOrDefault("scope"));
        var request = RegisterCiba(pendingRequests, client, scopes, issuer);

        if (ResolveUser(userStore, loginHint) is { } user)
        {
            pendingRequests.Approve(request.Handle, user.UserName, user.Subject, DateTimeOffset.UtcNow);
        }

        return Results.Json(
            WantsUserCode(values)
                ? new CibaResponse(
                    request.Handle,
                    (int)CibaLifetime.TotalSeconds,
                    CibaIntervalInSeconds,
                    UserCodeGenerator.New(),
                    VerificationUri.Constant)
                : new CibaResponse(request.Handle, (int)CibaLifetime.TotalSeconds, CibaIntervalInSeconds));
    }

    /// <summary>
    /// El codigo de usuario es opcional en CIBA: solo se devuelve si el cliente declara que lo admite
    /// con <c>user_code_parameter_supported</c>, como hacen los que lo muestran en su pantalla para
    /// que el usuario pueda distinguir la peticion.
    /// </summary>
    private static bool WantsUserCode(IReadOnlyDictionary<string, string> values) =>
        string.Equals(
            values.GetValueOrDefault("user_code_parameter_supported"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static PendingAuthorizationRequest RegisterCiba(
        IPendingAuthorizationStore pendingRequests,
        Client client,
        IReadOnlyList<string> scopes,
        string issuer) =>
        pendingRequests.Issue(new PendingAuthorizationRequest(
            OpaqueToken.New(),
            client.ClientId,
            scopes,
            new ValidatedAuthorizationRequest(
                client, string.Empty, scopes, ResponseTypeNames.Code, ResponseModes.Query,
                Nonce: null, State: null, CodeChallenge: null, CodeChallengeMethod: null, Prompt: null),
            issuer,
            DateTimeOffset.UtcNow + CibaLifetime,
            TimeSpan.FromSeconds(CibaIntervalInSeconds)));

    private static User? ResolveUser(IUserStore userStore, string loginHint) =>
        userStore.FindByUserName(loginHint) ?? userStore.FindBySubject(loginHint);

    private static IResult UnknownClient() =>
        ProtocolErrorResults.From(ProtocolErrors.InvalidClient("El cliente no es valido."));

    private static string[] Scopes(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? ["openid"]
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>Cuerpo de la respuesta de device authorization (RFC 8628 3.2).</summary>
public sealed record DeviceAuthorizationBody(
    [property: JsonPropertyName("device_code")] string DeviceCode,
    [property: JsonPropertyName("user_code")] string UserCode,
    [property: JsonPropertyName("verification_uri")] string VerificationUri,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("interval")] int Interval);

/// <summary>
/// Cuerpo de la respuesta de CIBA. Los dos ultimos campos solo se rellenan cuando el cliente ha
/// pedido el codigo de usuario, y por eso son opcionales en la forma serializada.
/// </summary>
public sealed record CibaResponse(
    [property: JsonPropertyName("auth_req_id")] string AuthReqId,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("interval")] int Interval,
    [property: JsonPropertyName("user_code")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? UserCode = null,
    [property: JsonPropertyName("verification_uri")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? VerificationUri = null);
