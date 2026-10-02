using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Core.Scopes;

namespace OidcMock.Core.Discovery;

/// <summary>
/// Construye el documento de discovery announcing unicamente la superficie que el mock implementa.
/// Los scopes y los claims salen del IScopeStore, el resto son capacidades fijas del mock.
/// </summary>
public sealed class DiscoveryDocumentBuilder(IScopeStore scopeStore, OidcMockOptions options, ClientAuthenticator clientAuthenticator)
{
    private static readonly string[] GrantTypes =
    [
        "authorization_code",
        "client_credentials",
        "refresh_token",
        "implicit",
        "password",
        "urn:ietf:params:oauth:grant-type:device_code",
        "urn:openid:params:grant-type:ciba"
    ];

    private static readonly string[] ResponseTypes =
    [
        "code",
        "token",
        "id_token",
        "id_token token",
        "code id_token",
        "code token",
        "code id_token token"
    ];

    private static readonly string[] ResponseModes = ["form_post", "query", "fragment"];

    private static readonly string[] PromptValues = ["none", "login", "consent", "select_account"];

    private static readonly string[] CodeChallengeMethods = ["plain", "S256"];

    public DiscoveryDocument Build(string issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

        var normalizedIssuer = EndpointUri.NormalizeIssuer(issuer);
        var scopes = scopeStore.List();

        // Los metodos de autenticacion se anuncian desde los autenticadores registrados, para que el
        // discovery no pueda quedar por detras de lo que el token endpoint acepta de verdad.
        IReadOnlyList<string> authMethods = clientAuthenticator.SupportedMethods;

        return new DiscoveryDocument
        {
            Issuer = normalizedIssuer,
            JwksUri = EndpointUri.Combine(normalizedIssuer, EndpointPaths.Jwks),
            AuthorizationEndpoint = EndpointUri.Combine(normalizedIssuer, EndpointPaths.Authorize),
            TokenEndpoint = EndpointUri.Combine(normalizedIssuer, EndpointPaths.Token),
            UserInfoEndpoint = EndpointUri.Combine(normalizedIssuer, EndpointPaths.UserInfo),
            EndSessionEndpoint = EndpointUri.Combine(normalizedIssuer, EndpointPaths.EndSession),
            CheckSessionIframe = EndpointUri.Combine(normalizedIssuer, EndpointPaths.CheckSession),
            RevocationEndpoint = EndpointUri.Combine(normalizedIssuer, EndpointPaths.Revocation),
            IntrospectionEndpoint = EndpointUri.Combine(normalizedIssuer, EndpointPaths.Introspection),
            DeviceAuthorizationEndpoint = EndpointUri.Combine(normalizedIssuer, EndpointPaths.DeviceAuthorization),
            BackchannelAuthenticationEndpoint = EndpointUri.Combine(normalizedIssuer, EndpointPaths.Ciba),
            PushedAuthorizationRequestEndpoint = EndpointUri.Combine(normalizedIssuer, EndpointPaths.PushedAuthorizationRequest),
            RequirePushedAuthorizationRequests = false,
            ScopesSupported = [.. scopes.Select(scope => scope.Name)],
            ClaimsSupported = CollectClaims(scopes),
            GrantTypesSupported = GrantTypes,
            ResponseTypesSupported = ResponseTypes,
            ResponseModesSupported = ResponseModes,
            TokenEndpointAuthMethodsSupported = authMethods,
            RevocationEndpointAuthMethodsSupported = authMethods,
            IntrospectionEndpointAuthMethodsSupported = authMethods,
            IdTokenSigningAlgValuesSupported = DiscoveryCapabilities.SigningAlgorithms,
            SubjectTypesSupported = DiscoveryCapabilities.SubjectTypes,
            CodeChallengeMethodsSupported = CodeChallengeMethods,
            PromptValuesSupported = PromptValues,
            AuthorizationResponseIssParameterSupported = true,
            BackchannelTokenDeliveryModesSupported = DiscoveryCapabilities.BackchannelTokenDeliveryModes,
            BackchannelUserCodeParameterSupported = true,
            FrontchannelLogoutSupported = true
        };
    }

    /// <summary>
    /// Issuer anunciado: el configurado, o el que se deduce del host de la peticion mas el PathBase.
    /// </summary>
    public string ResolveIssuer(string scheme, string host) =>
        EndpointUri.NormalizeIssuer(options.Issuer ?? $"{scheme}://{host}{EndpointUri.NormalizePathBase(options.PathBase)}");

    private static string[] CollectClaims(IReadOnlyList<ScopeDefinition> scopes) =>
    [.. scopes.SelectMany(scope => scope.Claims).Distinct(StringComparer.Ordinal)];
}
