using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Cookie de sesion del mock. Es propia, no la de ASP.NET, porque el unico estado que necesita
/// es "este navegador ya se autentico como alguien": una cookie firmada por el framework
/// obligaria a configurar autenticacion completa para un mock.
/// </summary>
public static class AuthSessionCookie
{
    public const string Name = "oidc_mock_session";

    public static string? Read(HttpRequest request) => request.Cookies.TryGetValue(Name, out var value)
        ? value
        : null;

    public static void Write(HttpResponse response, string sessionId, OidcMockOptions options, TimeSpan lifetime) =>
        response.Cookies.Append(Name, sessionId, new CookieOptions
        {
            Path = EndpointUri.NormalizePathBase(options.PathBase),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow + lifetime
        });

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(Name);
}
