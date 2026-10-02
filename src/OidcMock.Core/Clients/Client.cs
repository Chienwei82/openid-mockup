namespace OidcMock.Core.Clients;

/// <summary>
/// Cliente OAuth registrado en el mock. Inmutable: la configuracion se recarga creating nuevas instancias.
/// </summary>
public sealed record Client(
    string ClientId,
    string? ClientSecret,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    bool RequirePkce,
    bool RequireClientSecret,
    TokenLifetimes TokenLifetimes,
    Branding Branding,
    string? FrontchannelLogoutUri = null)
{
    /// <summary>
    /// URL donde el cliente recibe el aviso de frontchannel logout (OpenID Connect Front-Channel
    /// Logout 3). Es opcional: si el cliente no la declara, el mock no tiene a quien avisar.
    /// </summary>
    public bool SupportsFrontchannelLogout => !string.IsNullOrEmpty(FrontchannelLogoutUri);
    public bool AllowsGrantType(string grantType) => Contains(AllowedGrantTypes, grantType);

    public bool AllowsRedirectUri(string redirectUri) => Contains(RedirectUris, redirectUri);

    public bool AllowsPostLogoutRedirectUri(string redirectUri) => Contains(PostLogoutRedirectUris, redirectUri);

    public bool AllowsScope(string scope) => Contains(AllowedScopes, scope);

    private static bool Contains(IReadOnlyList<string> registeredValues, string value) =>
        registeredValues.Contains(value, StringComparer.Ordinal);
}
