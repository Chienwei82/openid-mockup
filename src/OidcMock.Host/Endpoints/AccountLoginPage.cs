using System.Text.Encodings.Web;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Pantalla de login de la ruta /Account/Login, la entrada que usa el servidor real con su
/// parametro ReturnUrl. El formulario es propio del mock: usuario, contrasena y el ReturnUrl
/// oculto; la pantalla real es la de ASP.NET Identity y su HTML no se imita.
/// </summary>
public static class AccountLoginPage
{
    public const string ReturnUrlField = "ReturnUrl";

    public static string Render(string returnUrl, string? error) =>
        $$"""
          <!DOCTYPE html>
          <html lang="es">
          <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>OidcMock</title>
            <style>
              body { font-family: system-ui, sans-serif; background: #f4f5f7; margin: 0; padding: 3rem 1rem; }
              .card { max-width: 26rem; margin: 0 auto; background: #fff; border-radius: 12px; padding: 2rem;
                      box-shadow: 0 1px 3px rgba(0,0,0,.16); }
              header { border-bottom: 4px solid #00695C; margin: -2rem -2rem 1.5rem; padding: 1.25rem 2rem; }
              header .mock { font-size: .75rem; text-transform: uppercase; letter-spacing: .08em; color: #6b7280; }
              h1 { font-size: 1.1rem; margin: 0; color: #00695C; }
              label { display: block; font-size: .85rem; margin: 1rem 0 .25rem; }
              input { width: 100%; padding: .55rem; border: 1px solid #cbd5e1; border-radius: 6px; box-sizing: border-box; }
              button { width: 100%; margin-top: 1.5rem; padding: .65rem; border: 0; border-radius: 6px;
                       font-size: .95rem; cursor: pointer; background: #00695C; color: #fff; }
              .error { color: #b91c1c; font-size: .85rem; margin: 1rem 0 0; }
            </style>
          </head>
          <body>
            <div class="card">
              <header>
                <div class="mock">OidcMock</div>
                <h1>Identifícate</h1>
              </header>
              <form method="post" action="">
                <input type="hidden" name="{{ReturnUrlField}}" value="{{HtmlEncoder.Default.Encode(returnUrl)}}" />
                <label for="username">Usuario</label>
                <input id="username" name="{{LoginFormFields.UserName}}" autocomplete="username" required />
                <label for="password">Contraseña</label>
                <input id="password" name="{{LoginFormFields.Password}}" type="password" autocomplete="current-password" required />
                {{ErrorLine(error)}}
                <button type="submit">Iniciar sesión</button>
              </form>
            </div>
          </body>
          </html>
          """;

    private static string ErrorLine(string? error) =>
        string.IsNullOrEmpty(error) ? string.Empty : $"""<p class="error">{HtmlEncoder.Default.Encode(error)}</p>""";
}