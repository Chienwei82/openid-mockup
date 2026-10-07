using System.Text.Encodings.Web;
using OidcMock.Core.Authorization;
using OidcMock.Core.Clients;
using OidcMock.Core.Discovery;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Pantalla de detalle que muestra, para un cliente, la URL de connect/authorize/callback con sus
/// valores precargados. La URL (y el code_verifier de PKCE, si aplica) van en campos de solo lectura
/// seleccionables, con un boton para copiarlos.
/// </summary>
public static class AuthorizeUrlPage
{
    private const string UrlFieldId = "authorize-url";
    private const string VerifierFieldId = "code-verifier";

    public static string Render(Client client, DemoAuthorizeUrl generated, string pathBase)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(generated);

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>OidcMock - URL de authorize</title>
              {MockStyles.Render(client.Branding.PrimaryColor)}
            </head>
            <body>
              <div class="card wide">
                <header>
                  <div class="mock">OidcMock</div>
                  <h1>URL de authorize</h1>
                </header>
                <p>Cliente <strong>{Escape(client.Branding.DisplayName)}</strong> (<code>{Escape(client.ClientId)}</code>). Pega la URL en el navegador para empezar el login.</p>
                {CopyBlock(UrlFieldId, "URL para el navegador", generated.Url)}
                {VerifierBlock(generated.CodeVerifier)}
                <div class="actions">
                  <a class="deny" href="{Escape(pathBase)}">Volver a los clientes</a>
                </div>
              </div>
            </body>
            </html>
            """;
    }

    /// <summary>
    /// Los clientes sin redirect_uri (client_credentials) no tienen flujo de authorize: se explica en
    /// vez de mostrar una URL que no llevaria a ningun sitio.
    /// </summary>
    public static string RenderUnsupported(Client client, string pathBase)
    {
        ArgumentNullException.ThrowIfNull(client);

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>OidcMock - URL de authorize</title>
              {MockStyles.Render(client.Branding.PrimaryColor)}
            </head>
            <body>
              <div class="card wide">
                <header>
                  <div class="mock">OidcMock</div>
                  <h1>Sin flujo de authorize</h1>
                </header>
                <p>El cliente <strong>{Escape(client.Branding.DisplayName)}</strong> (<code>{Escape(client.ClientId)}</code>) no registra ningun redirect_uri: usa <code>client_credentials</code>, no el flujo de login.</p>
                <div class="actions">
                  <a class="deny" href="{Escape(pathBase)}">Volver a los clientes</a>
                </div>
              </div>
            </body>
            </html>
            """;
    }

    private static string VerifierBlock(string? verifier) =>
        verifier is null
            ? string.Empty
            : CopyBlock(VerifierFieldId, "code_verifier (se canjea junto al code)", verifier);

    private static string CopyBlock(string id, string label, string value) =>
        $"""
            <label for="{id}">{Escape(label)}</label>
            <textarea id="{id}" class="url-box" readonly onclick="this.select()">{Escape(value)}</textarea>
            <button class="generate" type="button" onclick="navigator.clipboard.writeText(document.getElementById('{id}').value)">Copiar</button>
            """;

    private static string Escape(string value) => HtmlEncoder.Default.Encode(value);
}
