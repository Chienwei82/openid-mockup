using Microsoft.AspNetCore.Http;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Lee los parametros de una peticion mezclando query string y formulario, para que un endpoint acepte
/// los dos metodos (por ejemplo, userinfo con GET o POST) sin duplicar la extraccion.
/// </summary>
public static class RequestValues
{
    public static async Task<IReadOnlyDictionary<string, string>> ReadAsync(HttpRequest request)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var parameter in request.Query)
        {
            values[parameter.Key] = parameter.Value.ToString();
        }

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();

            foreach (var field in form)
            {
                values[field.Key] = field.Value.ToString();
            }
        }

        return values;
    }

    /// <summary>Access token del encabezado Authorization: Bearer, como en RFC 6750.</summary>
    public static string? ReadBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";

        return header.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? header[bearerPrefix.Length..].Trim()
            : null;
    }
}