namespace OidcMock.Core.Tokens;

/// <summary>
/// Valores JOSE que el mock usa al firmar sus tokens.
/// </summary>
public static class TokenHeaderValues
{
    public const string IdTokenType = "JWT";
    public const string AccessTokenType = "at+jwt";
    public const string SignatureAlgorithm = "RS256";
}