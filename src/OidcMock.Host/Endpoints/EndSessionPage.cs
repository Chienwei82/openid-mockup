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
              {{MockStyles.Render(primaryColor: null)}}
            </head>
            <body>
              <div class="card centered">
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