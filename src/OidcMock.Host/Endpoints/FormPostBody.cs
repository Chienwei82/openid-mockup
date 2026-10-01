using System.Text.Encodings.Web;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Cuerpo HTML del response_mode=form_post: autoenvia los parametros de la respuesta al redirect_uri
/// del cliente mediante un formulario, tal como describe OAuth 2.0 Multiple Response Types.
/// </summary>
public static class FormPostBody
{
    public static string Render(string redirectUri, IReadOnlyDictionary<string, string> parameters)
    {
        var inputs = string.Join(
            Environment.NewLine,
            parameters.Select(parameter => $"  <input type=\"hidden\" name=\"{Escape(parameter.Key)}\" value=\"{Escape(parameter.Value)}\" />"));

        return $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head><meta charset="utf-8" /><title>Enviando respuesta de autorizacion</title></head>
            <body onload="document.forms[0].submit()">
            <form method="post" action="{{Escape(redirectUri)}}">
            {{inputs}}
              <noscript><button type="submit">Continuar</button></noscript>
            </form>
            </body>
            </html>
            """;
    }

    private static string Escape(string value) => HtmlEncoder.Default.Encode(value);
}