namespace OidcMock.Core.Authorization;

/// <summary>
/// Peticion de autorizacion tal como llega del cliente, ya deserializada pero sin validar.
/// </summary>
public sealed record AuthorizationRequest(
    string? ClientId,
    string? RedirectUri,
    string? ResponseType,
    IReadOnlyList<string> Scopes,
    string? Nonce,
    string? State,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    string? Prompt,
    string? GrantType,
    string ResponseMode)
{
    /// <summary>
    /// Scopes con los que se valida la peticion. Sin scope la peticion se interpreta como la
    /// minima de OIDC (solo openid), que es lo que hacen los authorization endpoints de verdad.
    /// </summary>
    public IReadOnlyList<string> EffectiveScopes =>
        Scopes.Count > 0 ? Scopes : [Core.Scopes.ScopeNames.OpenId];
}