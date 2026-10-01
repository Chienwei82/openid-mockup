using System.Text.Json;
using System.Text.Json.Serialization;

namespace OidcMock.Core.Discovery;

/// <summary>
/// Documento de discovery del mock. Los nombres de campo replican los del servidor real y solo
/// incluye lo que el mock implementa; el orden de las propiedades es el orden de serializacion.
/// </summary>
public sealed record DiscoveryDocument
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("jwks_uri")]
    public required string JwksUri { get; init; }

    [JsonPropertyName("authorization_endpoint")]
    public required string AuthorizationEndpoint { get; init; }

    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; init; }

    [JsonPropertyName("userinfo_endpoint")]
    public required string UserInfoEndpoint { get; init; }

    [JsonPropertyName("end_session_endpoint")]
    public required string EndSessionEndpoint { get; init; }

    [JsonPropertyName("check_session_iframe")]
    public required string CheckSessionIframe { get; init; }

    [JsonPropertyName("revocation_endpoint")]
    public required string RevocationEndpoint { get; init; }

    [JsonPropertyName("introspection_endpoint")]
    public required string IntrospectionEndpoint { get; init; }

    [JsonPropertyName("device_authorization_endpoint")]
    public required string DeviceAuthorizationEndpoint { get; init; }

    [JsonPropertyName("backchannel_authentication_endpoint")]
    public required string BackchannelAuthenticationEndpoint { get; init; }

    [JsonPropertyName("pushed_authorization_request_endpoint")]
    public required string PushedAuthorizationRequestEndpoint { get; init; }

    [JsonPropertyName("require_pushed_authorization_requests")]
    public bool RequirePushedAuthorizationRequests { get; init; }

    [JsonPropertyName("scopes_supported")]
    public required IReadOnlyList<string> ScopesSupported { get; init; }

    [JsonPropertyName("claims_supported")]
    public required IReadOnlyList<string> ClaimsSupported { get; init; }

    [JsonPropertyName("grant_types_supported")]
    public required IReadOnlyList<string> GrantTypesSupported { get; init; }

    [JsonPropertyName("response_types_supported")]
    public required IReadOnlyList<string> ResponseTypesSupported { get; init; }

    [JsonPropertyName("response_modes_supported")]
    public required IReadOnlyList<string> ResponseModesSupported { get; init; }

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public required IReadOnlyList<string> TokenEndpointAuthMethodsSupported { get; init; }

    [JsonPropertyName("revocation_endpoint_auth_methods_supported")]
    public required IReadOnlyList<string> RevocationEndpointAuthMethodsSupported { get; init; }

    [JsonPropertyName("introspection_endpoint_auth_methods_supported")]
    public required IReadOnlyList<string> IntrospectionEndpointAuthMethodsSupported { get; init; }

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public required IReadOnlyList<string> IdTokenSigningAlgValuesSupported { get; init; }

    [JsonPropertyName("subject_types_supported")]
    public required IReadOnlyList<string> SubjectTypesSupported { get; init; }

    [JsonPropertyName("code_challenge_methods_supported")]
    public required IReadOnlyList<string> CodeChallengeMethodsSupported { get; init; }

    [JsonPropertyName("prompt_values_supported")]
    public required IReadOnlyList<string> PromptValuesSupported { get; init; }

    [JsonPropertyName("authorization_response_iss_parameter_supported")]
    public bool AuthorizationResponseIssParameterSupported { get; init; }

    [JsonPropertyName("backchannel_token_delivery_modes_supported")]
    public required IReadOnlyList<string> BackchannelTokenDeliveryModesSupported { get; init; }

    [JsonPropertyName("backchannel_user_code_parameter_supported")]
    public bool BackchannelUserCodeParameterSupported { get; init; }

    /// <summary>
    /// Todas las URLs del documento, para verificar que cuelgan del issuer.
    /// </summary>
    public IReadOnlyList<string> Paths() =>
    [
        Issuer,
        JwksUri,
        AuthorizationEndpoint,
        TokenEndpoint,
        UserInfoEndpoint,
        EndSessionEndpoint,
        CheckSessionIframe,
        RevocationEndpoint,
        IntrospectionEndpoint,
        DeviceAuthorizationEndpoint,
        BackchannelAuthenticationEndpoint,
        PushedAuthorizationRequestEndpoint
    ];
}
