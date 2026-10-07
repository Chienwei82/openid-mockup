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

    /// <summary>
    /// El path lo resuelve el llamante a partir de la peticion (<see cref="IssuerResolver.PathBase"/>),
    /// porque bajo IIS la base real la pone el propio IIS y puede no coincidir con la configurada.
    /// </summary>
    public static void Write(HttpResponse response, string sessionId, string path, TimeSpan lifetime) =>
        response.Cookies.Append(Name, sessionId, BuildOptions(path, lifetime));

    /// <summary>
    /// El borrado declara las mismas opciones que la escritura, sobre todo el <c>path</c>: el
    /// navegador empareja una cookie por (nombre, dominio, path), asi que borrar sin el <c>path</c> del
    /// PathBase genera un Set-Cookie que no empareja con la cookie escrita y la sesion sobrevive al
    /// logout en el navegador.
    /// </summary>
    public static void Clear(HttpResponse response, string path) =>
        response.Cookies.Delete(Name, BuildOptions(path, lifetime: null));

    /// <summary>
    /// Opciones de la cookie, unicas para escribir y borrar: cambiar solo una de las dos veces rompe el
    /// emparejamiento por nombre+path que hace que el borrado sirva de algo.
    /// </summary>
    private static CookieOptions BuildOptions(string path, TimeSpan? lifetime) =>
        new()
        {
            Path = path,
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            // Sin caducidad el borrado no pone expires, y es CookieOptions.Delete quien escribe el
            // expires en el pasado que hace que el navegador descarte la cookie.
            Expires = lifetime is { } remaining ? DateTimeOffset.UtcNow + remaining : null
        };
}
