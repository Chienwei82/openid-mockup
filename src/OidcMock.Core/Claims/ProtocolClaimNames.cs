namespace OidcMock.Core.Claims;

/// <summary>
/// Nombres de los claims de protocolo que el mock emite en sus tokens.
/// </summary>
public static class ProtocolClaimNames
{
    public const string Issuer = "iss";
    public const string Subject = "sub";
    public const string Audience = "aud";
    public const string Expiration = "exp";
    public const string IssuedAt = "iat";
    public const string NotBefore = "nbf";
    public const string AuthTime = "auth_time";
    public const string Nonce = "nonce";
    public const string AccessTokenHash = "at_hash";
    public const string AuthorizationCodeHash = "c_hash";
    public const string Scope = "scope";
    public const string ClientId = "client_id";
    public const string TokenId = "jti";
    public const string SessionId = "sid";
    public const string IdentityProvider = "idp";
    public const string AuthenticationContext = "acr";
    public const string AuthenticationMethods = "amr";
}