using System.Text.Json;

namespace OidcMock.Host.Stores;

/// <summary>
/// Ajustes comunes de deserializacion de los archivos de configuracion del mock.
/// </summary>
internal static class JsonConfiguration
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
