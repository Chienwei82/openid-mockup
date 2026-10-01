namespace OidcMock.Core.Discovery;

/// <summary>
/// Rutas relativas de los endpoints del mock, tal como las expone el servidor real.
/// </summary>
public static class EndpointPaths
{
    public const string Authorize = "connect/authorize";
    public const string Token = "connect/token";
    public const string UserInfo = "connect/userinfo";
    public const string EndSession = "connect/endsession";
    public const string CheckSession = "connect/checksession";
    public const string Revocation = "connect/revocation";
    public const string Introspection = "connect/introspect";
    public const string DeviceAuthorization = "connect/deviceauthorization";
    public const string Ciba = "connect/ciba";
    public const string PushedAuthorizationRequest = "connect/par";
    public const string Jwks = ".well-known/openid-configuration/jwks";
    public const string Configuration = ".well-known/openid-configuration";
}
