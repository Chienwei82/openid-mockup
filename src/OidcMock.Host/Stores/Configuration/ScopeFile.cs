using System.Text.Json.Serialization;

namespace OidcMock.Host.Stores.Configuration;

/// <summary>
/// Forma del archivo scopes.json.
/// </summary>
internal sealed class ScopeFile
{
    [JsonPropertyName("scopes")]
    public List<ScopeEntry>? Scopes { get; init; }
}

internal sealed class ScopeEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("claims")]
    public List<string>? Claims { get; init; }
}
