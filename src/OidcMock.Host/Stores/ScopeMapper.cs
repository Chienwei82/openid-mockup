using OidcMock.Core.Configuration;
using OidcMock.Core.Scopes;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Traduce el contenido de scopes.json al modelo de dominio.
/// </summary>
internal static class ScopeMapper
{
    public static IReadOnlyList<ScopeDefinition> ToDomain(ScopeFile file)
    {
        var entries = file.Scopes ?? throw new ConfigurationException(
            ConfigurationFiles.Scopes,
            "falta la coleccion 'scopes'.");

        return entries.Select(ToDomain).ToArray();
    }

    private static ScopeDefinition ToDomain(ScopeEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            throw new ConfigurationException(ConfigurationFiles.Scopes, "un scope no tiene 'name' (o esta vacio).");
        }

        return new ScopeDefinition(entry.Name, entry.Claims ?? []);
    }
}
