namespace OidcMock.Host.Endpoints;

/// <summary>
/// Decodifica el encabezado Authorization de client_secret_basic, que viaja como
/// base64(urlencode(client_id) + ":" + urlencode(client_secret)) (RFC 6749 2.3.1). Un encabezado que
/// no es Basic, o un base64 corrupto, no son credenciales: el endpoint los ignora y la peticion
/// acaba sin cliente, que es invalid_client.
/// </summary>
public static class BasicAuthorizationHeader
{
    private const string BasicPrefix = "Basic ";

    public static bool TryRead(HttpRequest request, out (string? ClientId, string? Secret) credentials)
    {
        credentials = (null, null);

        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith(BasicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(header[BasicPrefix.Length..].Trim()));
            var separator = decoded.IndexOf(':', StringComparison.Ordinal);

            credentials = separator < 0
                ? (decoded, null)
                : (decoded[..separator], decoded[(separator + 1)..]);

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}