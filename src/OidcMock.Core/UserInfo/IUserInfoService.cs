using OidcMock.Core.Errors;
using OidcMock.Core.Users;

namespace OidcMock.Core.UserInfo;

/// <summary>
/// Devuelve los claims del usuario que el access token permite ver, para el endpoint userinfo.
/// </summary>
public interface IUserInfoService
{
    Result<IReadOnlyDictionary<string, System.Text.Json.JsonElement>> Describe(
        string accessToken,
        string issuer,
        IEnumerable<string> audiences);
}

/// <summary>
/// Construye la respuesta de /connect/userinfo a partir del subject del token y de los scopes que
/// el token lleva: el usuario solo ve lo que el token autorizo.
/// </summary>
public interface IUserInfoClaimsSource
{
    Result<IReadOnlyDictionary<string, System.Text.Json.JsonElement>> Describe(User user, IReadOnlyList<string> scopes);
}