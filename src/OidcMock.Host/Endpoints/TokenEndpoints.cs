using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using OidcMock.Core.Introspection;
using OidcMock.Core.Revocation;
using OidcMock.Core.UserInfo;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Endpoints que negocian o consumen tokens: token, userinfo, introspect, revocation y end_session.
/// </summary>
public static class TokenEndpoints
{
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<OidcMockOptions>();
        var group = endpoints.MapGroup(EndpointUri.NormalizePathBase(options.PathBase));

        group.MapPost(EndpointPaths.Token, IssueToken);
        group.MapGet(EndpointPaths.UserInfo, DescribeUserInfo);
        group.MapPost(EndpointPaths.UserInfo, DescribeUserInfo);
        group.MapPost(EndpointPaths.Introspection, Introspect);
        group.MapPost(EndpointPaths.Revocation, Revoke);
        group.MapGet(EndpointPaths.EndSession, EndSession);
        group.MapPost(EndpointPaths.EndSession, EndSession);

        return endpoints;
    }

    private static async Task<IResult> IssueToken(
        HttpContext context,
        ITokenEndpointService tokenEndpoint,
        DiscoveryDocumentBuilder discoveryBuilder,
        OidcMockOptions options)
    {
        var result = tokenEndpoint.IssueToken(
            await BindTokenRequestAsync(context.Request),
            IssuerResolver.Resolve(context.Request, discoveryBuilder, options));

        return result.Succeeded
            ? Results.Json(result.Value, TokenResponse.SerializerOptions)
            : ErrorResponse(result.Error!);
    }

    private static IResult DescribeUserInfo(
        HttpContext context,
        IUserInfoService userInfo,
        DiscoveryDocumentBuilder discoveryBuilder,
        OidcMockOptions options)
    {
        var result = userInfo.Describe(
            RequestValues.ReadBearerToken(context.Request) ?? string.Empty,
            IssuerResolver.Resolve(context.Request, discoveryBuilder, options),
            audiences: null);

        return result.Succeeded ? Results.Json(result.Value) : ErrorResponse(result.Error!);
    }

    private static async Task<IResult> Introspect(
        HttpContext context,
        IIntrospectionService introspection,
        DiscoveryDocumentBuilder discoveryBuilder,
        OidcMockOptions options)
    {
        var values = await RequestValues.ReadAsync(context.Request);
        var clientId = values.GetValueOrDefault("client_id") ?? string.Empty;

        var result = introspection.Introspect(
            values.GetValueOrDefault("token") ?? string.Empty,
            IssuerResolver.Resolve(context.Request, discoveryBuilder, options),
            [clientId],
            clientId);

        return Results.Json(result.Value, IntrospectionResponse.SerializerOptions);
    }

    private static async Task<IResult> Revoke(
        HttpContext context,
        ITokenRevocationService revocation,
        DiscoveryDocumentBuilder discoveryBuilder,
        OidcMockOptions options)
    {
        var values = await RequestValues.ReadAsync(context.Request);
        var clientId = values.GetValueOrDefault("client_id") ?? string.Empty;

        var result = revocation.Revoke(
            values.GetValueOrDefault("token") ?? string.Empty,
            IssuerResolver.Resolve(context.Request, discoveryBuilder, options),
            [clientId],
            clientId);

        return result.Failed ? ErrorResponse(result.Error!) : Results.Empty;
    }

    /// <summary>
    /// end_session del mock: valida el post_logout_redirect_uri contra los clientes registrados y
    /// redirige. No hay estado de sesion que cerrar, asi que terminar la sesion es no hacer nada mas.
    /// </summary>
    private static async Task<IResult> EndSession(HttpContext context, IClientStore clientStore)
    {
        var values = await RequestValues.ReadAsync(context.Request);
        var redirectUri = values.GetValueOrDefault("post_logout_redirect_uri");

        if (!IsRegisteredPostLogoutUri(clientStore, values, redirectUri))
        {
            return ErrorResponse(new Core.Errors.ProtocolError(
                "invalid_request",
                "El post_logout_redirect_uri no esta registrado para el cliente.",
                Core.Errors.ProtocolErrors.BadRequest));
        }

        var state = values.GetValueOrDefault("state");

        return Results.Redirect(
            string.IsNullOrEmpty(state)
                ? redirectUri!
                : QueryStringHelper.AppendQuery(redirectUri!, new Dictionary<string, string> { ["state"] = state }));
    }

    private static bool IsRegisteredPostLogoutUri(
        IClientStore clientStore,
        IReadOnlyDictionary<string, string> values,
        string? redirectUri)
    {
        if (string.IsNullOrEmpty(redirectUri))
        {
            return false;
        }

        var clientId = values.GetValueOrDefault("client_id");

        return string.IsNullOrEmpty(clientId)
            ? clientStore.List().Any(client => client.AllowsPostLogoutRedirectUri(redirectUri))
            : clientStore.Find(clientId)?.AllowsPostLogoutRedirectUri(redirectUri) == true;
    }

    private static async Task<TokenEndpointRequest> BindTokenRequestAsync(HttpRequest request)
    {
        var values = await RequestValues.ReadAsync(request);

        return new TokenEndpointRequest(
            values.GetValueOrDefault("client_id") ?? ReadBasicAuthClientId(request),
            values.GetValueOrDefault("client_secret") ?? ReadBasicAuthSecret(request),
            values.GetValueOrDefault("grant_type"),
            SplitScopes(values.GetValueOrDefault("scope")),
            values.GetValueOrDefault("code"),
            values.GetValueOrDefault("redirect_uri"),
            values.GetValueOrDefault("code_verifier"),
            values.GetValueOrDefault("refresh_token"),
            values.GetValueOrDefault("username"),
            values.GetValueOrDefault("password"));
    }

    private static string[] SplitScopes(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? []
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// client_secret_basic viaja en el encabezado Authorization como base64(client_id:secret), y
    /// client_secret_post en el cuerpo. Se aceptan los dos, como anuncia el discovery.
    /// </summary>
    private static (string? ClientId, string? Secret) ReadBasicAuth(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        const string basicPrefix = "Basic ";

        if (!header.StartsWith(basicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(header[basicPrefix.Length..].Trim()));
            var separator = decoded.IndexOf(':', StringComparison.Ordinal);

            return separator < 0
                ? (decoded, null)
                : (decoded[..separator], decoded[(separator + 1)..]);
        }
        catch (FormatException)
        {
            return (null, null);
        }
    }

    private static string? ReadBasicAuthClientId(HttpRequest request) => ReadBasicAuth(request).ClientId;

    private static string? ReadBasicAuthSecret(HttpRequest request) => ReadBasicAuth(request).Secret;

    private static IResult ErrorResponse(Core.Errors.ProtocolError error) =>
        Results.Json(
            new TokenErrorResponse { Error = error.Code, ErrorDescription = error.Description },
            TokenResponse.SerializerOptions,
            statusCode: error.StatusCode);
}
