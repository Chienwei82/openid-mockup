using System.Text.Json;
using OidcMock.Core.Users;

namespace OidcMock.Core.Claims;

/// <summary>
/// Decide que claims de un usuario viajan en un token segun los scopes autorizados.
/// </summary>
public interface IClaimsProjector
{
    IReadOnlyDictionary<string, JsonElement> Project(User user, IReadOnlyList<string> grantedScopes);
}