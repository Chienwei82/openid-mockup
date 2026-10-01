using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Traduce el contenido de clients.json al modelo de dominio.
/// </summary>
internal static class ClientMapper
{
    public static IReadOnlyList<Client> ToDomain(ClientFile file)
    {
        var entries = file.Clients ?? throw new ConfigurationException(
            ConfigurationFiles.Clients,
            "falta la coleccion 'clients'.");

        return entries.Select(ToDomain).ToArray();
    }

    private static Client ToDomain(ClientEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ClientId))
        {
            throw new ConfigurationException(
                ConfigurationFiles.Clients,
                "un cliente no tiene 'client_id' (o esta vacio).");
        }

        return new Client(
            entry.ClientId,
            entry.ClientSecret,
            entry.RedirectUris ?? [],
            entry.PostLogoutRedirectUris ?? [],
            entry.AllowedGrantTypes ?? [],
            entry.AllowedScopes ?? [],
            entry.RequirePkce,
            entry.RequireClientSecret,
            ToTokenLifetimes(entry.TokenLifetimes),
            ToBranding(entry.Branding));
    }

    private static TokenLifetimes ToTokenLifetimes(TokenLifetimesEntry? entry) =>
        entry is null
            ? throw new ConfigurationException(
                ConfigurationFiles.Clients,
                "un cliente no tiene el bloque 'token_lifetimes'.")
            : new TokenLifetimes(
                entry.AccessToken,
                entry.IdentityToken,
                entry.RefreshToken,
                entry.AuthorizationCode);

    private static Branding ToBranding(BrandingEntry? entry) =>
        new(entry?.DisplayName ?? string.Empty, entry?.LogoUrl, entry?.PrimaryColor);
}
