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
        Func<string, string> perfilHref,
        bool chooser)
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
              {{MockStyles.Render(branding.PrimaryColor)}}
            </head>
            <body>
              <div class="card wide">
                <header>
                  <div class="mock">OidcMock</div>
                  <div class="identity">
                    {{logo}}
                    <h1>{{Escape(branding.DisplayName)}}</h1>
                  </div>
                </header>
                <p>{{Intro(authorization, chooser)}}</p>
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
    /// El copy del papel que cumple la pantalla: login pide acceso, la eleccion de cuenta invita a
    /// escoger entre las de users.json.
    /// </summary>
    private static string Intro(ValidatedAuthorizationRequest authorization, bool chooser) =>
        chooser
            ? $"Elige una cuenta para entrar a <strong>{Escape(authorization.Client.ClientId)}</strong>."
            : $"La aplicacion <strong>{Escape(authorization.Client.ClientId)}</strong> solicita acceso a tu cuenta.";

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