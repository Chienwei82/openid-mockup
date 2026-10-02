using OidcMock.Core.Claims;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Lectura de claims de un token ya validado. El nombre del claim llega por parametro porque el access
/// token y el id token comparten esta lectura y solo cambian los claims que les interesan. Asumir que
/// el claim existe es del lector: quien decide es <see cref="SignedTokenValidator"/>.
/// </summary>
public static class TokenClaimReader
{
    public static string? Read(IDictionary<string, object> claims, string claimName) =>
        claims.TryGetValue(claimName, out var value) ? value?.ToString() : null;

    public static string ReadRequired(IDictionary<string, object> claims, string claimName) =>
        Read(claims, claimName) ?? string.Empty;

    public static DateTimeOffset ReadExpiry(IDictionary<string, object> claims) =>
        long.TryParse(
            Read(claims, ProtocolClaimNames.Expiration),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : DateTimeOffset.UnixEpoch;
}