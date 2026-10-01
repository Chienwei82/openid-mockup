using System.Text.Json;
using OidcMock.Core.Claims;
using OidcMock.Core.Errors;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;

namespace OidcMock.Core.UserInfo;

/// <summary>
/// Caso de uso de /connect/userinfo: valida el access token, localiza al usuario y devuelve solo los
/// claims que los scopes del token autorizan.
/// </summary>
public sealed class UserInfoService(
    IAccessTokenReader accessTokenReader,
    IUserInfoClaimsSource claimsSource,
    IUserStore userStore) : IUserInfoService
{
    public Result<IReadOnlyDictionary<string, JsonElement>> Describe(
        string accessToken,
        string issuer,
        IEnumerable<string>? audiences)
    {
        var token = accessTokenReader.Read(accessToken, issuer, audiences);
        if (token.Failed)
        {
            return Result<IReadOnlyDictionary<string, JsonElement>>.Fail(token.Error!);
        }

        var user = userStore.FindBySubject(token.Value!.Subject);
        if (user is null)
        {
            return Result<IReadOnlyDictionary<string, JsonElement>>.Fail(
                ProtocolErrors.InvalidToken("El subject del token no corresponde a ningun usuario del mock."));
        }

        return claimsSource.Describe(user, token.Value.Scopes);
    }
}