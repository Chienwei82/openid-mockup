namespace OidcMock.UnitTests.Discovery;

/// <summary>
/// Discovery del servidor real del BCCR, usado como contrato de forma: los nombres de campo del
/// mock deben ser un subconjunto de estos.
/// </summary>
public static class ReferenceDiscoveryDocument
{
    public const string Json = """
        {
          "issuer": "https://oauth2.bccr.fi.cr/personafisica/",
          "jwks_uri": "https://oauth2.bccr.fi.cr/personafisica/.well-known/openid-configuration/jwks",
          "authorization_endpoint": "https://oauth2.bccr.fi.cr/personafisica/connect/authorize",
          "token_endpoint": "https://oauth2.bccr.fi.cr/personafisica/connect/token",
          "userinfo_endpoint": "https://oauth2.bccr.fi.cr/personafisica/connect/userinfo",
          "end_session_endpoint": "https://oauth2.bccr.fi.cr/personafisica/connect/endsession",
          "check_session_iframe": "https://oauth2.bccr.fi.cr/personafisica/connect/checksession",
          "revocation_endpoint": "https://oauth2.bccr.fi.cr/personafisica/connect/revocation",
          "introspection_endpoint": "https://oauth2.bccr.fi.cr/personafisica/connect/introspect",
          "device_authorization_endpoint": "https://oauth2.bccr.fi.cr/personafisica/connect/deviceauthorization",
          "backchannel_authentication_endpoint": "https://oauth2.bccr.fi.cr/personafisica/connect/ciba",
          "pushed_authorization_request_endpoint": "https://oauth2.bccr.fi.cr/personafisica/connect/par",
          "require_pushed_authorization_requests": false,
          "frontchannel_logout_supported": true,
          "frontchannel_logout_session_supported": true,
          "backchannel_logout_supported": true,
          "backchannel_logout_session_supported": true,
          "scopes_supported": ["openid", "profile", "email", "offline_access"],
          "claims_supported": ["sub", "name", "email"],
          "grant_types_supported": ["authorization_code", "refresh_token", "implicit", "client_credentials", "password"],
          "response_types_supported": ["code", "token", "id_token", "id_token token", "code id_token", "code token", "code id_token token"],
          "response_modes_supported": ["form_post", "query", "fragment"],
          "token_endpoint_auth_methods_supported": ["client_secret_basic", "client_secret_post", "ClientCertificate"],
          "revocation_endpoint_auth_methods_supported": ["client_secret_basic", "client_secret_post", "ClientCertificate"],
          "introspection_endpoint_auth_methods_supported": ["client_secret_basic", "client_secret_post", "ClientCertificate"],
          "id_token_signing_alg_values_supported": ["RS256"],
          "userinfo_signing_alg_values_supported": ["RS256"],
          "introspection_signing_alg_values_supported": ["RS256"],
          "subject_types_supported": ["public"],
          "code_challenge_methods_supported": ["plain", "S256"],
          "request_parameter_supported": true,
          "request_object_signing_alg_values_supported": ["RS256", "ES256"],
          "prompt_values_supported": ["none", "login", "consent", "select_account"],
          "authorization_response_iss_parameter_supported": true,
          "backchannel_token_delivery_modes_supported": ["poll"],
          "backchannel_user_code_parameter_supported": true,
          "backchannel_authentication_request_signing_alg_values_supported": ["RS256", "ES256"],
          "dpop_signing_alg_values_supported": ["RS256", "ES256"]
        }
        """;

    public static HashSet<string> FieldNames { get; } = ReadFieldNames();

    private static HashSet<string> ReadFieldNames()
    {
        using var document = System.Text.Json.JsonDocument.Parse(Json);
        return document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
    }
}
