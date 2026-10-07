namespace OidcMock.Host.Endpoints;

/// <summary>
/// check_session_iframe (OpenID Connect Session Management 3). El cliente lo carga en un iframe oculto
/// y lo usa como canal de sesion. El mock no mantiene estado de sesion, asi que sirve la pagina vacia
/// con la que el cliente puede hacer polling sin errores de CORS.
/// </summary>
public static class CheckSessionPage
{
    /// <summary>
    /// Pagina vacia con la que el cliente puede hacer polling sin errores de CORS, con la misma hoja
    /// Material You que el resto de pantallas del mock.
    /// </summary>
    public static string Body =>
        $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8" /><title>check_session</title>{{MockStyles.Render(primaryColor: null)}}</head>
        <body>
        <script>
          // El mock no mantiene estado de sesion: se notifica el cambio en cuanto se carga el iframe,
          // que es el comportamiento que el cliente espera de un proveedor sin sesiones persistidas.
          function notify() {
            if (window.parent && window.parent !== window) {
              window.parent.postMessage('oidcmock.check_session', '*');
            }
          }
          notify();
        </script>
        </body>
        </html>
        """;
}