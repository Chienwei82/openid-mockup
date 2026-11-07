using System.Collections.Generic;
using System.Text.Json;
using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Extensiones de conversión entre los modelos de peticion de administracion (valores string)
/// y los tipos de dominio (JsonElement para claims, Client para clientes).
/// </summary>
public static class AdminExtensions
{
    public static Dictionary<string, JsonElement> ToJsonElements(this IReadOnlyDictionary<string, string> claims)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, value) in claims)
        {
            result[key] = JsonSerializer.SerializeToElement(value, AdminRequest.SerializerOptions);
        }
        return result;
    }

    public static Client ToClient(this AdminClientRequest request) => new(
        request.ClientId,
        request.ClientSecret,
        request.RedirectUris,
        [],
        request.AllowedGrantTypes,
        [],
        false,
        request.RequireClientSecret,
        request.TokenLifetimes,
        new Branding(
            string.Empty,
            null,
            null),
        null);
}
