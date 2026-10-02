using System.Text.Encodings.Web;
using OidcMock.Core.Authorization;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Pantalla de consentimiento del mock, la que aparece con prompt=consent cuando ya hay sesion.
/// No pide contrasena: solo enumera lo que el cliente pide y deja aceptar o denegar.
/// </summary>
public static class ConsentPage
{
    public const string DecisionField = "decision";
    public const string Allow = "allow";
    public const string Deny = "deny";

    public static string Render(ValidatedAuthorizationRequest authorization, string userName)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        var branding = authorization.Client.Branding;

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
                h1 { font-size: 1.1rem; margin: 0; color: var(--primary); }
                .mock { font-size: .75rem; text-transform: uppercase; letter-spacing: .08em; color: #6b7280; }
                ul { padding-left: 1.1rem; font-size: .9rem; }
                .actions { display: flex; gap: .75rem; margin-top: 1.5rem; }
                button { flex: 1; padding: .65rem; border: 0; border-radius: 6px; font-size: .95rem; cursor: pointer; }
                .accept { background: var(--primary); color: #fff; }
                .deny { background: #e5e7eb; color: #374151; }
              </style>
            </head>
            <body>
              <div class="card">
                <header>
                  <div class="mock">OidcMock</div>
                  <h1>{{Escape(branding.DisplayName)}}</h1>
                </header>
                <p><strong>{{Escape(userName)}}</strong>, la aplicacion <strong>{{Escape(authorization.Client.ClientId)}}</strong>
                   quiere acceder a tu cuenta.</p>
                <ul>{{ScopeItems(authorization)}}</ul>
                <form method="post" action="">
                  {{HiddenFields(authorization)}}
                  <div class="actions">
                    <button class="accept" type="submit" name="{{DecisionField}}" value="{{Allow}}">Permitir</button>
                    <button class="deny" type="submit" name="{{DecisionField}}" value="{{Deny}}">Denegar</button>
                  </div>
                </form>
              </div>
            </body>
            </html>
            """;
    }

    private static string ScopeItems(ValidatedAuthorizationRequest authorization) =>
        string.Join(
            Environment.NewLine,
            authorization.Scopes.Select(scope => $"<li><code>{Escape(scope)}</code></li>"));

    /// <summary>
    /// El POST del consentimiento debe reenviar la peticion original, asi que viaja oculta y
    /// completa, igual que en la pantalla de login.
    /// </summary>
    private static string HiddenFields(ValidatedAuthorizationRequest authorization)
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
            // La peticion empujada se reenvia oculta: el POST del consentimiento vuelve al authorize
            // y tiene que volver a encontrar los parametros originales detras del request_uri.
            [AuthorizationRequestBinder.RequestUriField] = authorization.RequestUri
        };

        return string.Join(
            Environment.NewLine,
            fields
                .Where(field => !string.IsNullOrEmpty(field.Value))
                .Select(field => $"<input type=\"hidden\" name=\"{Escape(field.Key)}\" value=\"{Escape(field.Value!)}\" />"));
    }

    private static string Escape(string value) => HtmlEncoder.Default.Encode(value);
}