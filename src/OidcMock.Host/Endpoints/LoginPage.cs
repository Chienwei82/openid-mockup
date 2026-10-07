using System.Net;
using System.Text.Encodings.Web;
using OidcMock.Core.Authorization;
using OidcMock.Core.Clients;
using OidcMock.Core.Users;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Pantalla de login del mock: un formulario con los datos de la peticion en campos ocultos y la
/// identidad visual del cliente. No pretende parecerse al servidor real, solo ser utilizable.
/// </summary>
public static class LoginPage
{
    public static string Render(
        ValidatedAuthorizationRequest authorization,
        IReadOnlyList<User> profiles,
        AuthorizationRequest request,
        User profile,
        Func<string, string> perfilHref)
    {
        var branding = authorization.Client.Branding;
        var hiddenFields = HiddenFields(authorization, request, profile);
        var identity = IdentityFields.Render(profile, profiles, perfilHref);
        var logo = Logo(branding);

        return $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>{{Escape(branding.DisplayName)}}</title>
              <style>
                :root { --primary: {{Escape(branding.PrimaryColor ?? "#00695C")}}; }
                body { font-family: system-ui, sans-serif; background: #f4f5f7; margin: 0; padding: 3rem 1rem; }
                .card { max-width: 26rem; margin: 0 auto; background: #fff; border-radius: 12px; padding: 2rem;
                        box-shadow: 0 1px 3px rgba(0,0,0,.16); }
                header { border-bottom: 4px solid var(--primary); margin: -2rem -2rem 1.5rem; padding: 1.25rem 2rem; }
                header .identity { display: flex; align-items: center; gap: .75rem; }
                header img { height: 2rem; }
                h1 { font-size: 1.1rem; margin: 0; color: var(--primary); }
                .mock { font-size: .75rem; text-transform: uppercase; letter-spacing: .08em; color: #6b7280; }
                label { display: block; font-size: .85rem; margin: 1rem 0 .25rem; }
                input[type=text], input[type=password], select { width: 100%; padding: .55rem; border: 1px solid #cbd5e1;
                        border-radius: 6px; box-sizing: border-box; }
                .actions { display: flex; gap: .75rem; margin-top: 1.5rem; }
                button { flex: 1; padding: .65rem; border: 0; border-radius: 6px; font-size: .95rem; cursor: pointer; }
                .accept { background: var(--primary); color: #fff; }
                .deny { background: #e5e7eb; color: #374151; }
                .scopes { margin: 1.25rem 0 0; padding: .75rem; background: #f8fafc; border-radius: 6px; font-size: .8rem; }
              </style>
            </head>
            <body>
              <div class="card">
                <header>
                  <div class="mock">OidcMock</div>
                  <div class="identity">
                    {{logo}}
                    <h1>{{Escape(branding.DisplayName)}}</h1>
                  </div>
                </header>
                <p>La aplicacion <strong>{{Escape(authorization.Client.ClientId)}}</strong> solicita acceso a tu cuenta.</p>
                <form method="post" action="">
                  {{hiddenFields}}
                  {{identity}}
                  <div class="scopes">Scopes solicitados: {{Escape(string.Join(" ", authorization.Scopes))}}</div>
                  <div class="actions">
                    <button class="accept" type="submit" name="{{LoginFormFields.Action}}" value="{{LoginFormFields.Accept}}">Aceptar</button>
                    <button class="deny" type="submit" name="{{LoginFormFields.Action}}" value="{{LoginFormFields.Deny}}">Denegar</button>
                  </div>
                </form>
              </div>
            </body>
            </html>
            """;
    }

    /// <summary>
    /// El POST del login debe reenviar la peticion original tal cual, asi que los campos ocultos son
    /// los mismos parametros que llegaron por query string, mas el perfil base cuyos valores se
    /// precargaron.
    /// </summary>
    private static string HiddenFields(ValidatedAuthorizationRequest authorization, AuthorizationRequest request, User profile)
    {
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["client_id"] = authorization.Client.ClientId,
            ["redirect_uri"] = authorization.RedirectUri,
            ["response_type"] = authorization.ResponseType,
            ["response_mode"] = authorization.ResponseMode,
            ["scope"] = string.Join(' ', authorization.Scopes),
            ["nonce"] = authorization.Nonce,
            ["state"] = authorization.State,
            ["prompt"] = authorization.Prompt,
            ["code_challenge"] = authorization.CodeChallenge,
            ["code_challenge_method"] = authorization.CodeChallengeMethod,
            ["grant_type"] = request.GrantType,
            [LoginFormFields.Perfil] = profile.UserName,
            // La peticion empujada se reenvia oculta: el POST del login vuelve al authorize y tiene
            // que volver a encontrar los parametros originales detras del request_uri.
            [AuthorizationRequestBinder.RequestUriField] = authorization.RequestUri
        };

        return string.Join(
            Environment.NewLine,
            fields
                .Where(field => !string.IsNullOrEmpty(field.Value))
                .Select(field => $"<input type=\"hidden\" name=\"{Escape(field.Key)}\" value=\"{Escape(field.Value!)}\" />"));
    }

    /// <summary>
    /// El logo del branding se sirve desde /assets; sin logo configurado, solo el nombre.
    /// </summary>
    private static string Logo(Branding branding) =>
        string.IsNullOrEmpty(branding.LogoUrl)
            ? string.Empty
            : $"<img src=\"{Escape(branding.LogoUrl)}\" alt=\"{Escape(branding.DisplayName)}\" />";

    private static string Escape(string value) => HtmlEncoder.Default.Encode(value);
}