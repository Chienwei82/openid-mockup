using System.Text.Encodings.Web;
using OidcMock.Core.Clients;
using OidcMock.Core.Discovery;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Pantalla raiz del mock: en vez de rechazar la conexion, lista los clientes configurados y ofrece,
/// para los que usan el flujo de authorize, generar una URL lista para pegar en el navegador.
/// </summary>
public static class HomePage
{
    private const string DetailRoute = "authorize-url";

    public static string Render(IReadOnlyList<Client> clients, string pathBase)
    {
        ArgumentNullException.ThrowIfNull(clients);

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>OidcMock - Clientes</title>
              {MockStyles.Render(primaryColor: null)}
            </head>
            <body>
              <div class="card wide">
                <header>
                  <div class="mock">OidcMock</div>
                  <h1>Clientes configurados</h1>
                </header>
                <p>Elige un cliente para generar una URL de <code>connect/authorize/callback</code> con los valores precargados, lista para copiar.</p>
                <div class="clients">
                  {string.Join(Environment.NewLine, clients.Select(client => ClientCard(client, pathBase)))}
                </div>
              </div>
            </body>
            </html>
            """;
    }

    private static string ClientCard(Client client, string pathBase) =>
        $"""
            <article class="client">
              <h2>{Escape(client.Branding.DisplayName)}</h2>
              <dl>
                <dt>client_id</dt><dd><code>{Escape(client.ClientId)}</code></dd>
                <dt>redirect_uri</dt><dd>{RedirectUris(client)}</dd>
                <dt>grant types</dt><dd>{Join(client.AllowedGrantTypes)}</dd>
                <dt>scopes</dt><dd>{Join(client.AllowedScopes)}</dd>
                <dt>seguridad</dt><dd>{Security(client)}</dd>
              </dl>
              {Action(client, pathBase)}
            </article>
            """;

    private static string RedirectUris(Client client) =>
        client.RedirectUris.Count == 0 ? "(ninguno)" : Join(client.RedirectUris);

    private static string Security(Client client)
    {
        var values = new List<string>();
        if (client.RequirePkce)
        {
            values.Add("PKCE");
        }

        values.Add(client.RequireClientSecret ? "con secreto" : "publico");

        return string.Join(", ", values);
    }

    /// <summary>
    /// Solo los clientes del flujo de authorize pueden recibir una URL de login; los de
    /// client_credentials no tienen a donde redirigir, asi que se anota en vez de ofrecer el enlace.
    /// </summary>
    private static string Action(Client client, string pathBase) =>
        client.RedirectUris.Count == 0
            ? """<p class="hint">Este cliente no usa el flujo de authorize (client_credentials).</p>"""
            : $"""<a class="generate" href="{Escape(DetailHref(client.ClientId, pathBase))}">Generar URL de authorize</a>""";

    private static string DetailHref(string clientId, string pathBase) =>
        $"{EndpointUri.Combine(pathBase, DetailRoute)}?client={Uri.EscapeDataString(clientId)}";

    private static string Join(IReadOnlyList<string> values) => string.Join(", ", values);

    private static string Escape(string value) => HtmlEncoder.Default.Encode(value);
}
