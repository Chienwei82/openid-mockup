using System.Text.Encodings.Web;
using OidcMock.Core.EndSession;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Pagina de "sesion cerrada" de /connect/endsession. Es la respuesta que ve el usuario cuando el
/// cliente no trae post_logout_redirect_uri, y el sitio donde OpenID Connect Front-Channel Logout 3
/// exige cargar un iframe por cada cliente con frontchannel_logout_uri, con <c>iss</c> y <c>sid</c>.
/// El iframe no es decorativo: es la unica senal de que la sesion del cliente se termino.
/// </summary>
public static class EndSessionPage
{
    public static string Render(EndSessionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Sesi&#243;n cerrada</title>
              <style>
                :root { --primary: #00695C; }
                body { font-family: system-ui, sans-serif; background: #f4f5f7; margin: 0; padding: 3rem 1rem; }
                .card { max-width: 26rem; margin: 0 auto; background: #fff; border-radius: 12px; padding: 2rem;
                        box-shadow: 0 1px 3px rgba(0,0,0,.16); text-align: center; }
                header { border-bottom: 4px solid var(--primary); margin: -2rem -2rem 1.5rem; padding: 1.25rem 2rem; }
                h1 { font-size: 1.1rem; margin: 0; color: var(--primary); }
                .mock { font-size: .75rem; text-transform: uppercase; letter-spacing: .08em; color: #6b7280; }
                p { color: #374151; font-size: .95rem; }
                iframe { display: none; }
              </style>
            </head>
            <body>
              <div class="card">
                <header>
                  <div class="mock">OidcMock</div>
                  <h1>Sesi&#243;n cerrada</h1>
                </header>
                <p>Has cerrado la sesi&#243;n en OidcMock. Puedes volver a entrar cuando quieras.</p>
              </div>
              {{FrontchannelLogouts(result)}}
            </body>
            </html>
            """;
    }

    /// <summary>
    /// Un iframe por cliente que declaro frontchannel_logout_uri, con el issuer y el identificador de
    /// la sesion que se acaba de cerrar. Sin sesion cerrada no hay <c>sid</c>: el aviso se emite igual,
    /// porque lo que el cliente necesita saber es que se termino la sesion del navegador.
    /// </summary>
    private static string FrontchannelLogouts(EndSessionResult result) =>
        string.IsNullOrEmpty(result.FrontchannelLogoutUri)
            ? string.Empty
            : $"<iframe src=\"{Escape(FrontchannelUri(result))}\" title=\"frontchannel logout\"></iframe>";

    private static string FrontchannelUri(EndSessionResult result) =>
        QueryStringHelper.AppendQuery(
            result.FrontchannelLogoutUri!,
            new Dictionary<string, string>
            {
                ["iss"] = result.Issuer,
                ["sid"] = result.SessionId ?? string.Empty
            });

    private static string Escape(string value) => HtmlEncoder.Default.Encode(value);
}