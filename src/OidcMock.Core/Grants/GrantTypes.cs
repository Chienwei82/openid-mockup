namespace OidcMock.Core.Grants;

/// <summary>
/// Grants que el token endpoint admite, con el nombre exacto de la especificacion.
/// </summary>
public static class GrantTypes
{
    public const string AuthorizationCode = "authorization_code";
    public const string RefreshToken = "refresh_token";
    public const string ClientCredentials = "client_credentials";
    public const string Password = "password";
    public const string Implicit = "implicit";
    public const string DeviceCode = "urn:ietf:params:oauth:grant-type:device_code";
    public const string Ciba = "urn:openid:params:grant-type:ciba";

    public static readonly string[] Supported =
    [
        AuthorizationCode,
        RefreshToken,
        ClientCredentials,
        Password,
        Implicit,
        DeviceCode,
        Ciba
    ];
}