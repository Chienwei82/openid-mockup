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
    private OidcClientHost? _client;

    private CompatibilityWorld(MockHostFixture mock, CompatibilityConfig config)
    {
        Mock = mock;
        _config = config;
    }

    public MockHostFixture Mock { get; }

    /// <summary>URL base del cliente OIDC. Nulo hasta que se llama a <see cref="StartClientAsync"/>.</summary>
    public string ClientBaseAddress => _client?.BaseAddress ?? throw new InvalidOperationException("El cliente no ha arrancado.");

    /// <summary>Servicios del cliente, para leer sus opciones ya resueltas desde las pruebas.</summary>
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

        return new CompatibilityWorld(mock, config);
    }

    public async Task StartClientAsync(params string[] scopes)
    {
        _client = await OidcClientHost.StartAsync(
            Mock.Issuer,
            ClientId,
            ClientSecret,
            scopes.Length == 0 ? DefaultScopes : scopes,
            TestContext.Current.CancellationToken);
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
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        Mock.Dispose();
        _config.Dispose();
    }
}