namespace OidcMock.Core.Crypto;

/// <summary>
/// Codificacion base64url (RFC 4648) usada por JOSE.
/// </summary>
public static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
