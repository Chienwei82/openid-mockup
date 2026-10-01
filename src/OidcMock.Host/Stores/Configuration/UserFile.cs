using System.Text.Json;
using System.Text.Json.Serialization;

namespace OidcMock.Host.Stores.Configuration;

/// <summary>
/// Forma del archivo users.json.
/// </summary>
internal sealed class UserFile
{
    [JsonPropertyName("users")]
    public List<UserEntry>? Users { get; init; }
}

internal sealed class UserEntry
{
    [JsonPropertyName("sub")]
    public string? Subject { get; init; }

    [JsonPropertyName("username")]
    public string? UserName { get; init; }

    [JsonPropertyName("password")]
    public string? Password { get; init; }

    [JsonPropertyName("claims")]
    public Dictionary<string, JsonElement>? Claims { get; init; }
}
