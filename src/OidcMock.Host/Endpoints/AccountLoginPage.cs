using System.Text.Encodings.Web;
using OidcMock.Core.Users;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Pantalla de identidad de la ruta /Account/Login, la entrada que usa el servidor real con su
/// parametro ReturnUrl. El formulario es propio del mock: subject y claims editables, el ReturnUrl
/// oculto y Aceptar/Denegar; la pantalla real es la de ASP.NET Identity y su HTML no se imita.
/// </summary>
public static class AccountLoginPage
{
    public const string ReturnUrlField = "ReturnUrl";

    public static string Render(
        string returnUrl,
        string? error,
        User profile,
        IReadOnlyList<User> profiles,
        Func<string, string> perfilHref) =>
        $$"""
          <!DOCTYPE html>
          <html lang="es">
          <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>OidcMock</title>
            {{MockStyles.Render(primaryColor: null)}}
          </head>
          <body>
            <div class="card wide">
              <header>
                <div class="mock">OidcMock</div>
                <h1>Identifícate</h1>
              </header>
              <form method="post" action="">
                <input type="hidden" name="{{ReturnUrlField}}" value="{{HtmlEncoder.Default.Encode(returnUrl)}}" />
                <input type="hidden" name="{{LoginFormFields.Perfil}}" value="{{HtmlEncoder.Default.Encode(profile.UserName)}}" />
                {{IdentityFields.Render(profile, profiles, perfilHref)}}
                {{ErrorLine(error)}}
                <div class="actions">
                  <button class="accept" type="submit" name="{{LoginFormFields.Action}}" value="{{LoginFormFields.Accept}}">Aceptar</button>
                  <button class="deny" type="submit" name="{{LoginFormFields.Action}}" value="{{LoginFormFields.Deny}}">Denegar</button>
                </div>
              </form>
            </div>
          </body>
          </html>
          """;

    private static string ErrorLine(string? error) =>
        string.IsNullOrEmpty(error) ? string.Empty : $"""<p class="error">{HtmlEncoder.Default.Encode(error)}</p>""";
}