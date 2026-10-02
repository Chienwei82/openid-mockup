using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Core.Errors;
using OidcMock.Host.Logging;
using OidcMock.Core.Grants;
using OidcMock.Core.Introspection;
using OidcMock.Core.Revocation;
using OidcMock.Core.UserInfo;
using OidcMock.Host.Cors;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Endpoints que negocian o consumen tokens: token, userinfo, introspect, revocation y end_session.
/// </summary>
public static class TokenEndpoints
{
    /// <summary>Nombre del parametro con el token, en /introspect y /revocation (RFC 7662, RFC 7009).</summary>
    private const string TokenField = "token";

    /// <summary>Nombre del parametro con el access token en /userinfo (RFC 6750 2.2 y 2.3).</summary>
    private const string AccessTokenField = "access_token";

    /// <summary>
    /// Categoria de log del token endpoint. Se nombra a mano porque la clase es estatica y no sirve
    /// como argumento de tipo para CreateLogger.
    /// </summary>
    public const string LogCategory = "OidcMock.Host.Endpoints.TokenEndpoints";

    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<OidcMockOptions>();
        var group = endpoints.MapGroup(EndpointUri.NormalizePathBase(options.PathBase));

        var browser = OidcMockCors.PolicyName;

        // El token endpoint y el userinfo los consume una SPA; introspect y revocation son de
        // backends que no hacen preflight, asi que no se exponen a otros origenes.
        group.MapPost(EndpointPaths.Token, IssueToken).RequireCors(browser);
        group.MapGet(EndpointPaths.UserInfo, DescribeUserInfo).RequireCors(browser);
        group.MapPost(EndpointPaths.UserInfo, DescribeUserInfo).RequireCors(browser);
        group.MapPost(EndpointPaths.Introspection, Introspect);
        group.MapPost(EndpointPaths.Revocation, Revoke);

        return endpoints;
    }

    private static async Task<IResult> IssueToken(
        HttpContext context,
        ITokenEndpointService tokenEndpoint,
        DiscoveryDocumentBuilder discoveryBuilder,
        ILoggerFactory loggerFactory,
        OidcMockOptions options)
    {
        var logger = loggerFactory.CreateLogger(LogCategory);
        var request = await BindTokenRequestAsync(context.Request);
        var result = await tokenEndpoint.IssueTokenAsync(
            request,
            IssuerResolver.Resolve(context.Request, discoveryBuilder, options));

        LogOutcome(logger, request, result);

        return result.Succeeded
            ? TokenResponseWithoutCaching(context, Results.Json(result.Value, TokenResponse.SerializerOptions))
            : TokenResponseWithoutCaching(context, ErrorResponse(result.Error!));
    }

    /// <summary>
    /// Se registra el resultado con metadatos (grant, cliente, caducidad) y nunca con el token, el
    /// refresh token ni el secreto: lo que se emite es una credencial viva.
    /// </summary>
    private static void LogOutcome(
        ILogger logger,
        TokenEndpointRequest request,
        Result<TokenResponse> result)
    {
        if (result.Succeeded)
        {
            var issued = result.Value!;
            OidcMockLog.TokensIssued(
                logger,
                request.GrantType ?? "(sin grant_type)",
                request.Credentials.ClientId ?? "(sin client_id)",
                (int)issued.ExpiresIn,
                issued.RefreshToken is not null);
            return;
        }

        OidcMockLog.TokenRequestRejected(
            logger,
            result.Error!.Code,
            request.Credentials.ClientId ?? "(sin client_id)",
            request.Credentials.Method);
    }

    /// <summary>
    /// Ninguna respuesta del token endpoint se guarda en cache: RFC 6749 5.1 lo exige para el access
    /// token, el id_token y el refresh_token. El error tambien, para que un 401 por secreto
    /// equivocado no quede cacheado por un proxy. Pragma: no-cache es el equivalente para los caches
    /// HTTP/1.0.
    /// </summary>
    private static IResult TokenResponseWithoutCaching(HttpContext context, IResult result)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache";
        context.Response.Headers.Pragma = "no-cache";

        return result;
    }

    /// <summary>
    /// /connect/userinfo acepta el Bearer en el encabezado (RFC 6750 2.1) y, cuando no hay, en el
    /// cuerpo o en el query string (RFC 6750 2.2 y 2.3), que es lo que hacen los clientes que no
    /// pueden poner cabeceras. El encabezado manda: si vino, es la credencial que el cliente eligio.
    /// </summary>
    private static async Task<IResult> DescribeUserInfo(
        HttpContext context,
        IUserInfoService userInfo,
        DiscoveryDocumentBuilder discoveryBuilder,
        OidcMockOptions options)
    {
        var values = await RequestValues.ReadAsync(context.Request);
        var accessToken = RequestValues.ReadBearerToken(context.Request) ??
            values.GetValueOrDefault(AccessTokenField);

        if (string.IsNullOrEmpty(accessToken))
        {
            return BearerChallengeResults.WithoutCredentials(context);
        }

        var result = userInfo.Describe(
            accessToken,
            IssuerResolver.Resolve(context.Request, discoveryBuilder, options),
            // La audiencia es el client_id del propio token, todavia desconocido: se valida la firma,
            // el issuer y la vigencia, que es lo que importa para autorizar la llamada.
            audiences: null);

        return result.Succeeded
            ? Results.Json(result.Value)
            : BearerChallengeResults.InvalidToken(context, result.Error!);
    }

    /// <summary>
    /// RFC 7662 2.1: la introspection exige cliente autenticado. Sin ella, cualquiera podria preguntar
    /// por el estado de cualquier token del mock, asi que un cliente ausente o con secreto erroneo es
    /// invalid_client (401) y no una respuesta con active=false.
    /// </summary>
    private static async Task<IResult> Introspect(
        HttpContext context,
        IIntrospectionService introspection,
        ClientAuthenticator clientAuthenticator,
        DiscoveryDocumentBuilder discoveryBuilder,
        OidcMockOptions options)
    {
        var values = await RequestValues.ReadAsync(context.Request);
        var client = AuthenticateClient(clientAuthenticator, context.Request, values);
        if (client is null)
        {
            return ProtocolErrorResults.From(InvalidClient());
        }

        var result = introspection.Introspect(
            values.GetValueOrDefault(TokenField) ?? string.Empty,
            IssuerResolver.Resolve(context.Request, discoveryBuilder, options),
            [client.ClientId],
            client.ClientId);

        return Results.Json(result.Value, IntrospectionResponse.SerializerOptions);
    }

    /// <summary>
    /// RFC 7009 2.1: la revocacion tambien exige cliente autenticado, y por el mismo motivo que la
    /// introspection: sin ella, quien conociera el client_id de una victima podria cerrarle las sesiones.
    /// El token desconocido se responde 200 (RFC 7009 2.2), para no revelar si existio.
    /// </summary>
    private static async Task<IResult> Revoke(
        HttpContext context,
        ITokenRevocationService revocation,
        ClientAuthenticator clientAuthenticator,
        DiscoveryDocumentBuilder discoveryBuilder,
        OidcMockOptions options)
    {
        var values = await RequestValues.ReadAsync(context.Request);
        var client = AuthenticateClient(clientAuthenticator, context.Request, values);
        if (client is null)
        {
            return ProtocolErrorResults.From(InvalidClient());
        }

        var result = revocation.Revoke(
            values.GetValueOrDefault(TokenField) ?? string.Empty,
            IssuerResolver.Resolve(context.Request, discoveryBuilder, options),
            [client.ClientId],
            client.ClientId);

        return result.Failed ? ErrorResponse(result.Error!) : Results.Empty;
    }

    private static Client? AuthenticateClient(
        ClientAuthenticator clientAuthenticator,
        HttpRequest request,
        IReadOnlyDictionary<string, string> values) =>
        clientAuthenticator.Authenticate(ClientCredentialsReader.Read(request, values));

    private static ProtocolError InvalidClient() =>
        ProtocolErrors.InvalidClient("El cliente no se ha autenticado correctamente.");

    private static async Task<TokenEndpointRequest> BindTokenRequestAsync(HttpRequest request)
    {
        var values = await RequestValues.ReadAsync(request);

        return new TokenEndpointRequest(
            ReadClientCredentials(request, values),
            values.GetValueOrDefault("grant_type"),
            SplitScopes(values.GetValueOrDefault("scope")),
            values.GetValueOrDefault("code"),
            values.GetValueOrDefault("redirect_uri"),
            values.GetValueOrDefault("code_verifier"),
            values.GetValueOrDefault("refresh_token"),
            values.GetValueOrDefault("username"),
            values.GetValueOrDefault("password"),
            // Los flujos por sondeo usan device_code (RFC 8628) o auth_req_id (CIBA) como handle.
            values.GetValueOrDefault("device_code") ?? values.GetValueOrDefault("auth_req_id"));
    }

    private static string[] SplitScopes(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? []
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// El encabezado Authorization tiene prioridad sobre el cuerpo (RFC 6749 2.3.1): si el cliente
    /// uso client_secret_basic, el client_secret del cuerpo no debe autenticarlo. Sin encabezado se
    /// anuncia client_secret_post, que es tambien lo que hacen los clientes publicos, que solo se
    /// identifican por client_id.
    /// </summary>
    private static ClientCredentials ReadClientCredentials(
        HttpRequest request,
        IReadOnlyDictionary<string, string> values) =>
        BasicAuthorizationHeader.TryRead(request, out var fromHeader)
            ? new ClientCredentials(ClientAuthenticationMethods.ClientSecretBasic, fromHeader.ClientId, fromHeader.Secret)
            : new ClientCredentials(
                ClientAuthenticationMethods.ClientSecretPost,
                values.GetValueOrDefault("client_id"),
                values.GetValueOrDefault("client_secret"));

    private static IResult ErrorResponse(Core.Errors.ProtocolError error) =>
        Results.Json(
            new TokenErrorResponse { Error = error.Code, ErrorDescription = error.Description },
            TokenResponse.SerializerOptions,
            statusCode: error.StatusCode);
}
