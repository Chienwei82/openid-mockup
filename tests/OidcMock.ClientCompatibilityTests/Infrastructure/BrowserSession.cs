using System.Net;
using System.Text.RegularExpressions;

namespace OidcMock.ClientCompatibilityTests.Infrastructure;

/// <summary>
/// Navegador minimo para las pruebas de protocolo: sigue redirecciones a mano, lleva las cookies y
/// sabe rellenar el formulario de login del mock.
///
/// No se usa <c>AllowAutoRedirect</c> porque el objetivo es <b>ver</b> cada paso: que el authorize
/// mande al login, que el login mande un codigo al <c>redirect_uri</c> registrado y que el logout
/// vuelva por donde debe. Un cliente que sigue las redirecciones solo devolveria la ultima pagina, y
/// un fallo en cualquier paso intermedio pasaria por un 200 final.
/// </summary>
public sealed partial class BrowserSession : IDisposable
{
    private const int MaxRedirects = 12;

    private readonly HttpClient _client;

    public BrowserSession()
    {
        _client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
            UseCookies = true
        });
    }

    /// <summary>Ultima respuesta recibida. Se consulta para comprobar en que paso se quedo el flujo.</summary>
    public HttpResponseMessage LastResponse { get; private set; } = null!;

    /// <summary>URL efectiva en la que termino la ultima navegacion.</summary>
    public string CurrentUrl { get; private set; } = string.Empty;

    /// <summary>GET que sigue las redirecciones hasta llegar a una pagina final.</summary>
    public Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken) =>
        FollowAsync(url, cancellationToken);

    /// <summary>
    /// Recorre el flujo completo contra el mock: entra al cliente, rellena el login que el mock
    /// devuelve y sigue hasta el endpoint protegido. Devuelve la respuesta de ese endpoint final.
    /// </summary>
    public async Task<HttpResponseMessage> SignInAsync(
        string clientBaseAddress,
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        await FollowAsync($"{clientBaseAddress}/login", cancellationToken);
        await SignInAtMockAsync(userName, password, cancellationToken);

        return await FollowAsync(CurrentUrl, cancellationToken);
    }

    /// <summary>
    /// Envia las credenciales al login del mock y deja la sesion lista. El authorize responde con el
    /// <c>redirect_uri</c> del cliente, que es el paso que hay que cruzar para tener un codigo.
    /// </summary>
    private async Task SignInAtMockAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var html = await LastResponse.Content.ReadAsStringAsync(cancellationToken);
        var fields = HiddenFields.Parse(html);

        fields["user"] = userName;
        fields["password"] = password;

        var authorizationUrl = CurrentUrl;

        await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, authorizationUrl)
            {
                Content = new FormUrlEncodedContent(fields)
            },
            cancellationToken);
    }
private async Task<HttpResponseMessage> FollowAsync(string url, CancellationToken cancellationToken)
    {
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            var response = await SendAsync(
                () => new HttpRequestMessage(HttpMethod.Get, url),
                cancellationToken);

            var next = ReadRedirect(response, url);
            if (next is null)
            {
                return response;
            }

            url = next;
        }

        throw new InvalidOperationException($"El flujo supero las {MaxRedirects} redirecciones y no termino.");
    }

    /// <summary>
    /// Envia una peticion y recuerda donde quedo el navegador. Una redireccion no es un error del
    /// flujo, asi que se devuelve tal cual y decide quien la sigue.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> request,
        CancellationToken cancellationToken)
    {
        var sent = request();
        var response = await _client.SendAsync(sent, cancellationToken);

        LastResponse = response;
        CurrentUrl = ReadRedirect(response, sent.RequestUri!.ToString()) ?? sent.RequestUri!.ToString();

        return response;
    }

    /// <summary>URL siguiente de una redireccion, o null si la respuesta no es una redireccion.</summary>
    private static string? ReadRedirect(HttpResponseMessage response, string currentUrl)
    {
        if (!IsRedirect(response.StatusCode))
        {
            return null;
        }

        var location = response.Headers.Location
            ?? throw new InvalidOperationException($"La redireccion desde '{currentUrl}' no traia Location.");

        return location.IsAbsoluteUri ? location.ToString() : new Uri(new Uri(currentUrl), location).ToString();
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther;

    public void Dispose() => _client.Dispose();
}

/// <summary>
/// Los campos ocultos que el mock renderiza en sus pantallas, leidos del HTML tal como haria el
/// navegador. El mock los emite uno por <c>input hidden</c>, que es lo unico que hace falta aqui.
/// </summary>
public static partial class HiddenFields
{
    public static Dictionary<string, string> Parse(string html)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match match in HiddenInputPattern().Matches(html))
        {
            fields[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        if (fields.Count == 0)
        {
            throw new InvalidOperationException("La pantalla del mock no traia los campos del formulario.");
        }

        return fields;
    }

    [GeneratedRegex(
        """<input[^>]*type=["']hidden["'][^>]*name=["'](?<name>[^"']+)["'][^>]*value=["'](?<value>[^"']*)["']""",
        RegexOptions.IgnoreCase)]
    private static partial Regex HiddenInputPattern();
}