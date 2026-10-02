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

    /// <summary>Recorrido de URLs y respuestas, para entender un fallo de redireccion.</summary>
    public List<string> Path { get; } = [];

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

        var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, authorizationUrl)
            {
                Content = new FormUrlEncodedContent(fields)
            },
            cancellationToken);

        // El login ya concedio: el authorize devuelve el codigo por form_post, y ese formulario es el
        // que hay que enviar para llegar al redirect_uri del cliente.
        var form = AutoSubmittedForm.Read(response)
            ?? throw new InvalidOperationException(
                $"El login del mock no devolvio un formulario de respuesta: {(int)response.StatusCode}." +
                $"{Environment.NewLine}{CollectingLoggerProvider.Dump()}");

        await SubmitAsync(form, cancellationToken);
    }

    /// <summary>
    /// Sigue el flujo como lo haria el navegador: GET y, si la pagina se autoenvia, POST al action y
    /// continuacion desde ahi. Sin ese POST el codigo se queda dentro del HTML y el cliente nunca
    /// recibe el callback.
    /// </summary>
    private async Task<HttpResponseMessage> FollowAsync(string url, CancellationToken cancellationToken)
    {
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            var response = await SendAsync(
                () => new HttpRequestMessage(HttpMethod.Get, url),
                cancellationToken);

            Path.Add($"GET {url} -> {(int)response.StatusCode}");

            if (AutoSubmittedForm.Read(response) is { } submitted)
            {
                await SubmitAsync(submitted, cancellationToken);
                continue;
            }

            if (ReadRedirect(response, url) is not { } next)
            {
                return response;
            }

            url = next;
        }

        throw new InvalidOperationException(
            $"El flujo supero las {MaxRedirects} redirecciones y no termino:{Environment.NewLine}" +
            string.Join(Environment.NewLine, Path));
    }

    /// <summary>
    /// Envia el formulario autoenviado y deja <see cref="CurrentUrl"/> en el destino siguiente: si la
    /// respuesta es una redireccion, en su <c>Location</c>; si no, en la URL a la que se envio.
    /// </summary>
    private async Task SubmitAsync(AutoSubmittedForm form, CancellationToken cancellationToken)
    {
        var action = new Uri(new Uri(CurrentUrl), form.Action);

        Path.Add($"POST {form.Action} (formulario autoenviado)");

        await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, action) { Content = new FormUrlEncodedContent(form.Fields) },
            cancellationToken);
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
/// Formulario que una pagina envia sola al cargar. El handler OpenIdConnect de .NET usa
/// <c>response_mode=form_post</c> por defecto en el flujo de codigo, y el mock lo responde con una
/// pagina cuyo <c>onload</c> hace <c>document.forms[0].submit()</c>. Un navegador real lo ejecutaria;
/// estas pruebas lo ejecutan por el, porque de lo contrario se quedarian con el codigo dentro del HTML.
/// </summary>
public sealed partial record AutoSubmittedForm(string Action, Dictionary<string, string> Fields)
{
    /// <summary>El formulario de la respuesta, o null si la pagina no se autoenvia.</summary>
    public static AutoSubmittedForm? Read(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentType?.MediaType is not "text/html")
        {
            return null;
        }

        var html = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        if (!AutoSubmitPattern().IsMatch(html))
        {
            return null;
        }

        return new AutoSubmittedForm(
            FormActionPattern().Match(html).Groups["action"].Value,
            HiddenFields.Parse(html));
    }

    /// <summary>Detecta el <c>onload</c> que autoenvia el formulario de la pagina.</summary>
    [GeneratedRegex("""<body[^>]*onload=["'][^"']*submit\(\)""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AutoSubmitPattern();

    [GeneratedRegex("""<form[^>]*action=["'](?<action>[^"']+)["']""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FormActionPattern();
}

/// <summary>
/// Los campos ocultos que el mock renderiza en sus pantallas, leidos del HTML tal como haria el
/// navegador. El mock los emite uno por <c>input hidden</c>, que es lo unico que hace falta aqui.
/// </summary>
public sealed partial class HiddenFields
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
            throw new InvalidOperationException(
                "La pagina del mock no traia los campos ocultos del formulario." +
                $"{Environment.NewLine}{CollectingLoggerProvider.Dump()}");
        }

        return fields;
    }

    [GeneratedRegex(
        """<input[^>]*type=["']hidden["'][^>]*name=["'](?<name>[^"']+)["'][^>]*value=["'](?<value>[^"']*)["']""",
        RegexOptions.IgnoreCase)]
    private static partial Regex HiddenInputPattern();
}