using System.Text.Json;
using OidcMock.Core.Users;

namespace OidcMock.Core.Claims;

/// <summary>
/// Resuelve <c>sub</c> con el Subject del usuario, que no vive en su diccionario de claims.
/// </summary>
public sealed class SubjectClaimSource : IUserClaimSource
{
    public bool TryResolve(string claimName, User user, out JsonElement value)
    {
        if (!string.Equals(claimName, ProtocolClaimNames.Subject, StringComparison.Ordinal))
        {
            value = default;

            return false;
        }

        value = JsonSerializer.SerializeToElement(user.Subject);

        return true;
    }
}

/// <summary>
/// Resuelve el claim con el valor homonimo del diccionario del usuario, conservando su tipo JSON.
/// </summary>
public sealed class UserDictionaryClaimSource : IUserClaimSource
{
    public bool TryResolve(string claimName, User user, out JsonElement value) => user.TryGetClaim(claimName, out value);
}