using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace OidcMock.ClientCompatibilityTests.Infrastructure;

/// <summary>
/// El mundo de una prueba de compatibilidad: el mock, la aplicacion cliente OIDC contra el y la
/// lectura del discovery.
///
/// Se crea por prueba porque cada una reserva sus puertos, levanta sus dos hosts y escribe su
/// <c>clients.json</c>: compartir un unico mundo entre pruebas las haria dependent del orden y
/// ocultaria en que estado quedo el mock.
/// </summary>
public sealed class CompatibilityWorld : IAsyncDisposable
{
    /// <summary>Cliente confidencial de los ejemplos del repositorio, el que usa PKCE y code.</summary>
    public const string ClientId = "web-app-confidencial";

    public const string ClientSecret = "super-secreto-web";

    public const string UserName = "jperez";

    public const string Password = "Passw0rd!";

    public const string UserSubject = "user-persona-fisica";

    /// <summary>Scopes minimos: openid para el id_token, offline_access para el refresh token.</summary>
    public static readonly string[] DefaultScopes = ["openid", "profile", "email", "offline_access"];

    private readonly CompatibilityConfig _config;
    private readonly LoopbackAddress _clientAddress;
    private OidcClientHost? _client;
    private JwtBearerApiHost? _api;

    private CompatibilityWorld(MockHostFixture mock, CompatibilityConfig config, LoopbackAddress clientAddress)
    {
        Mock = mock;
        _config = config;
        _clientAddress = clientAddress;
    }

    public MockHostFixture Mock { get; }

    /// <summary>
    /// URL base del cliente OIDC. Es la direccion reservada, no la del host arrancado: el
    /// <c>redirect_uri</c> se escribe en <c>clients.json</c> antes de que exista el cliente, asi que
    /// esta propiedad se puede consultar desde el principio.
    /// </summary>
    public string ClientBaseAddress => _clientAddress.BaseAddress;

    /// <summary>Servicios del cliente ya arrancado, para leer sus opciones resueltas.</summary>
    public IServiceProvider ClientServices =>
        _client?.Services ?? throw new InvalidOperationException("El cliente no ha arrancado.");

    public static async Task<CompatibilityWorld> StartAsync()
    {
        // Los puertos se reservan antes de escribir la configuracion: el redirect_uri del cliente
        // va en clients.json y el mock lo lee al arrancar.
        var clientAddress = LoopbackAddress.Reserve();
        var config = new CompatibilityConfig(clientAddress.BaseAddress, ClientId, ClientSecret);

        var mock = new MockHostFixture(LoopbackAddress.Reserve(), config);
        await mock.InitializeAsync();

        return new CompatibilityWorld(mock, config, clientAddress);
    }

    public async Task StartClientAsync(params string[] scopes)
    {
        _client = await OidcClientHost.StartAsync(
            _clientAddress,
            Mock.Issuer,
            ClientId,
            ClientSecret,
            scopes.Length == 0 ? DefaultScopes : scopes,
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Levanta la API protegida con JwtBearer, tambien configurada solo con el issuer: las claves y el
    /// emisor salen del discovery, y la audiencia del <c>client_id</c> del cliente de maquina a maquina.
    /// </summary>
    public async Task<JwtBearerApiHost> StartApiAsync()
    {
        _api = await JwtBearerApiHost.StartAsync(Mock.Issuer, TestContext.Current.CancellationToken);

        return _api;
    }

    /// <summary>
    /// El discovery se lee como lo haria el handler: con el ConfigurationManager de IdentityModel
    /// sobre la direccion que se construye a partir del Authority. Descargarlo a mano probaria el
    /// documento, no el camino que sigue el cliente de verdad.
    /// </summary>
    public Task<OpenIdConnectConfiguration> ReadConfigurationAsync()
    {
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{Mock.Issuer.TrimEnd('/')}/{MockEndpoints.Discovery}",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = false });

        return manager.GetConfigurationAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Documento de discovery tal cual lo sirve el mock, leido como JSON.</summary>
    public async Task<JsonElement> ReadDiscoveryDocumentAsync()
    {
        using var client = Mock.CreateApiClient();
        using var response = await client.GetAsync(
            MockEndpoints.Resolve(MockEndpoints.Discovery),
            TestContext.Current.CancellationToken);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.Clone();
    }

    public async ValueTask DisposeAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync();
        }

        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        Mock.Dispose();
        _config.Dispose();
    }
}