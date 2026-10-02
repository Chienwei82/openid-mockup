using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace OidcMock.ClientCompatibilityTests.Infrastructure;

/// <summary>
/// Renovacion de la sesion tal como la hace una aplicacion con OpenID Connect.
///
/// El handler de ASP.NET Core <b>no</b> canjea el refresh token por su cuenta: guarda el que llega en
/// la respuesta del token endpoint y es la aplicacion quien lo usa cuando quiere renovar. Este tipo
/// reproduce ese patron con las mismas piezas que tendria una aplicacion real:
/// <see cref="IAuthenticationService"/> para leer los tokens de la sesion, el token endpoint que el
/// handler ya-descubrio por metadata, y <see cref="JsonWebTokenHandler"/> para validar el id_token
/// nuevo contra las claves del discovery.
/// </summary>
public static class TokenRefresher
{
    public static async Task<RefreshOutcome> RefreshAsync(HttpContext context)
    {
        var session = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var refreshToken = session.Properties?.GetTokenValue(OpenIdConnectParameterNames.RefreshToken);

        if (string.IsNullOrEmpty(refreshToken))
        {
            return RefreshOutcome.Ko("La sesion no tiene refresh token.");
        }

        var options = context.RequestServices.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);

        var configuration = await options.ConfigurationManager!.GetConfigurationAsync(context.RequestAborted);
        var response = await RedeemAsync(options, configuration, refreshToken, context.RequestAborted);
        var body = await response.Content.ReadAsStringAsync(context.RequestAborted);

        if (!RefreshedTokens.TryRead(body, out var tokens))
        {
            return RefreshOutcome.Ko($"El token endpoint respondio {(int)response.StatusCode}: {body}");
        }

        var validation = await ValidateIdTokenAsync(tokens.IdToken, configuration, options.ClientId!);
        if (!validation.IsValid)
        {
            return RefreshOutcome.Ko($"El id_token renovado no valido: {validation.Exception?.Message}");
        }

        // La cookie se vuelve a emitir con los tokens nuevos: el canje por si solo no cambia la sesion.
        var properties = session.Properties!;
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = OpenIdConnectParameterNames.AccessToken, Value = tokens.AccessToken },
            new AuthenticationToken { Name = OpenIdConnectParameterNames.RefreshToken, Value = tokens.RefreshToken }
        ]);

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, session.Principal!, properties);

        return RefreshOutcome.Ok(new
        {
            sub = validation.ClaimsIdentity?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            access_token = tokens.AccessToken,
            refresh_token = tokens.RefreshToken
        });
    }
private static async Task<HttpResponseMessage> RedeemAsync(
        OpenIdConnectOptions options,
        OpenIdConnectConfiguration configuration,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = options.ClientId!,
                ["client_secret"] = options.ClientSecret!
            })
        };

        return await options.Backchannel.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Valida el id_token nuevo con la misma firma que el handler: issuer del discovery, firma contra
    /// las claves del JWKS y audiencia del cliente. Sin esto, un refresh que devolviera un token de
    /// otro emisor pasaria desapercibido.
    /// </summary>
    private static async Task<TokenValidationResult> ValidateIdTokenAsync(
        string idToken,
        OpenIdConnectConfiguration configuration,
        string clientId)
    {
        var handler = new JsonWebTokenHandler();

        return await handler.ValidateTokenAsync(
            idToken,
            new TokenValidationParameters
            {
                ValidIssuer = configuration.Issuer,
                ValidAudience = clientId,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
            });
    }
}

/// <summary>
/// Resultado de una renovacion: los tokens nuevos o el motivo del fallo. Se nombran <c>Ok</c> y
/// <c>Ko</c> y no <c>Succeeded</c>/<c>Failed</c> para no chocar con las propiedades del record.
/// </summary>
public sealed record RefreshOutcome(bool Succeeded, object? Value, string? Error)
{
    public static RefreshOutcome Ok(object value) => new(true, value, null);

    public static RefreshOutcome Ko(string error) => new(false, null, error);
}

/// <summary>
/// Los tres campos de una respuesta de renovacion. Se leen a mano y no con OpenIdConnectMessage
/// porque el canje lo hace la propia aplicacion, no el handler, y aqui no hace falta el tipo.
/// </summary>
public sealed record RefreshedTokens(string AccessToken, string IdToken, string RefreshToken)
{
    public static bool TryRead(string body, out RefreshedTokens tokens)
    {
        tokens = null!;

        using var document = System.Text.Json.JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("access_token", out var accessToken) ||
            !document.RootElement.TryGetProperty("id_token", out var idToken) ||
            !document.RootElement.TryGetProperty("refresh_token", out var refreshToken))
        {
            return false;
        }

        tokens = new RefreshedTokens(accessToken.GetString()!, idToken.GetString()!, refreshToken.GetString()!);

        return true;
    }
}