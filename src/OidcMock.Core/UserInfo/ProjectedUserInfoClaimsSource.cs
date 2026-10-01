using System.Text.Json;
using OidcMock.Core.Claims;
using OidcMock.Core.Errors;
using OidcMock.Core.Users;

namespace OidcMock.Core.UserInfo;

/// <summary>
/// Claims de userinfo a partir de la proyeccion por scopes: exactamente los mismos claims que el
/// token lleva, para que un cliente vea la misma informacion en el id_token y en /userinfo.
/// </summary>
public sealed class ProjectedUserInfoClaimsSource(IClaimsProjector claimsProjector) : IUserInfoClaimsSource
{
    public Result<IReadOnlyDictionary<string, JsonElement>> Describe(User user, IReadOnlyList<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(scopes);

        return Result<IReadOnlyDictionary<string, JsonElement>>.Ok(claimsProjector.Project(user, scopes));
    }
}