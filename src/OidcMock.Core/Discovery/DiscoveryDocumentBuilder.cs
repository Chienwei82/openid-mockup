using OidcMock.Core.Authorization;
using OidcMock.Core.Clients;
using OidcMock.Core.Codes;
using OidcMock.Core.Configuration;
using OidcMock.Core.Grants;
using OidcMock.Core.Scopes;

namespace OidcMock.Core.Discovery;

/// <summary>
/// Construye el documento de discovery announcing unicamente la superficie que el mock implementa.
/// Los scopes y los claims salen del IScopeStore, el resto son capacidades fijas del mock.
/// </summary>
public sealed class DiscoveryDocumentBuilder(IScopeStore scopeStore, OidcMockOptions options, ClientAuthenticator clientAuthenticator)
{
    /// Los valores que el mock anuncia salen de las constantes del dominio (GrantTypes,
    /// ResponseTypeNames, ResponseModes, PromptValues y PkceCodeChallengeMethods) y no de copias
    /// literales de aqui: un grant o un prompt nuevo queda anunciado sin tocar el builder, y el
    /// discovery no puede quedarse atras respecto a lo que el endpoint acepta.
    ///
    /// Los metodos de autenticacion vienen de los autenticadores registrados, por el mismo motivo.
    /// </summary>
    private IReadOnlyList<string> AuthMethods => clientAuthenticator.SupportedMethods;

    public DiscoveryDocument Build(string issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

        var normalizedIssuer = EndpointUri.NormalizeIssuer(issuer);
        var scopes = scopeStore.List();

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
            GrantTypesSupported = GrantTypes.Supported,
            ResponseTypesSupported = ResponseTypeNames.SupportedCombinations,
            ResponseModesSupported = ResponseModes.Supported,
            TokenEndpointAuthMethodsSupported = AuthMethods,
            RevocationEndpointAuthMethodsSupported = AuthMethods,
            IntrospectionEndpointAuthMethodsSupported = AuthMethods,
            IdTokenSigningAlgValuesSupported = DiscoveryCapabilities.SigningAlgorithms,
            SubjectTypesSupported = DiscoveryCapabilities.SubjectTypes,
            CodeChallengeMethodsSupported = PkceCodeChallengeMethods.Supported,
            PromptValuesSupported = PromptValues.Supported,
            AuthorizationResponseIssParameterSupported = true,
            BackchannelTokenDeliveryModesSupported = DiscoveryCapabilities.BackchannelTokenDeliveryModes,
            BackchannelUserCodeParameterSupported = true,
            FrontchannelLogoutSupported = true
        };
    }

    /// <summary>
    /// Issuer anunciado: el configurado, o el que se deduce del host de la peticion mas el PathBase.
    /// </summary>
    public string ResolveIssuer(string scheme, string host, string pathBase) =>
        EndpointUri.NormalizeIssuer(options.EffectiveIssuer ?? $"{scheme}://{host}{EndpointUri.NormalizePathBase(pathBase)}");

    private static string[] CollectClaims(IReadOnlyList<ScopeDefinition> scopes) =>
    [.. scopes.SelectMany(scope => scope.Claims).Distinct(StringComparer.Ordinal)];
}
