using OidcMock.Core.Scopes;

namespace OidcMock.Core.Grants;

/// <summary>
/// Cuando un grant puede emitir un id_token. La regla es la misma para todos (OpenID Connect Core
/// 3.1.3.6): el id_token solo existe con el scope <c>openid</c>. Un canje que lo que sea sin ese
/// scope es OAuth a secas y devolver una identidad seria inventarla.
/// </summary>
public static class IdTokenRules
{
    public static bool GrantsIdToken(IReadOnlyList<string> scopes) =>
        scopes.Contains(ScopeNames.OpenId, StringComparer.Ordinal);
}
