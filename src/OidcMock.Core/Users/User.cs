using System.Text.Json;

namespace OidcMock.Core.Users;

/// <summary>
/// Usuario del mock con un diccionario de claims de nombres libres.
/// </summary>
public sealed record User(
    string Subject,
    string UserName,
    string Password,
    IReadOnlyDictionary<string, JsonElement> Claims)
{
    public bool TryGetClaim(string claimName, out JsonElement value) => Claims.TryGetValue(claimName, out value);

    public JsonElement GetClaim(string claimName) =>
        TryGetClaim(claimName, out var value)
            ? value
            : throw new ClaimNotFoundException(Subject, claimName);
}
