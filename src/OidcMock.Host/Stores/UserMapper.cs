using OidcMock.Core.Configuration;
using OidcMock.Core.Users;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Traduce el contenido de users.json al modelo de dominio.
/// </summary>
internal static class UserMapper
{
    public static IReadOnlyList<User> ToDomain(UserFile file)
    {
        var entries = file.Users ?? throw new ConfigurationException(
            ConfigurationFiles.Users,
            "falta la coleccion 'users'.");

        return entries.Select(ToDomain).ToArray();
    }

    private static User ToDomain(UserEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Subject))
        {
            throw new ConfigurationException(ConfigurationFiles.Users, "un usuario no tiene 'sub' (o esta vacio).");
        }

        if (string.IsNullOrWhiteSpace(entry.UserName))
        {
            throw new ConfigurationException(ConfigurationFiles.Users, "un usuario no tiene 'username' (o esta vacio).");
        }

        return new User(
            entry.Subject,
            entry.UserName,
            entry.Password ?? string.Empty,
            entry.Claims ?? new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal));
    }
}
