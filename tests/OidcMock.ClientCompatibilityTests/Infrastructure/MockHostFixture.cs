using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OidcMock.Host;

namespace OidcMock.ClientCompatibilityTests.Infrastructure;

/// <summary>
/// El mock levantado con <see cref="WebApplicationFactory{TEntryPoint}"/> pero **sobre Kestrel de
/// verdad**, no sobre el <c>TestServer</c>.
///
/// No es un detalle: el handler OpenIdConnect del host de prueba habla con el mock por HTTP real
/// (discovery, JWKS y canje del codigo) usando su propio backchannel. Un <c>TestServer</c> solo
/// existe dentro del proceso de pruebas y no es alcanzable por un <c>HttpClient</c> normal, asi que
/// con el los tests solo podrian ejercitar el mock contra si mismo.
///
/// Se escuchan dos URLs: una en loopback IPv4 y otra en <c>localhost</c>, porque Kestrel levanta el
/// servidor en cuanto se pide la primera direccion y las pruebas necesitan las dos.
///
/// El issuer se fija por configuracion en vez de deducirse del host: asi el cliente recibe un
/// <c>Authority</c> estable que se puede comprobar, y los tokens salen firmados con ese mismo valor.
/// </summary>
public sealed class MockHostFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly CompatibilityConfig? _config;
    private readonly LoopbackAddress _address;

    public MockHostFixture(LoopbackAddress address, CompatibilityConfig? config = null)
    {
        _address = address;
        _config = config;

        // Kestrel de verdad en el puerto reservado. Es la via que trae la propia
        // WebApplicationFactory en .NET 10; si se usara el TestServer (el valor por defecto), el
        // handler OpenIdConnect del host de prueba no podria ni descubrir el metadata ni canjear el
        // codigo, porque no habria ninguna URL que alcanzar.
        UseKestrel(address.Port);
    }

    /// <summary>Issuer del mock, con barra final, tal como lo anuncia el discovery.</summary>
    public string Issuer => $"{_address.BaseAddress}{MockEndpoints.PathBase}/";

    public ValueTask InitializeAsync()
    {
        // Se resuelve el servidor real para forzar el arranque: si el host se levanta con un error de
        // configuracion, sale aqui y no disfrazado de fallo en la primera peticion de un test.
        var server = Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];

        // El puerto se configuro a mano, asi que si Kestrel no lo respeta es un fallo de la
        // configuracion de escucha del test, no una prueba que valga: conviene decirlo claro.
        if (!addresses.Any(address => address.EndsWith($":{_address.Port}", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"El mock no escucha en el puerto reservado { _address.Port }. Escucha en: {string.Join(", ", addresses)}.");
        }

        return ValueTask.CompletedTask;
    }

    public new ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Se escucha por la configuracion del propio mock (OidcMock:Serving) y no con UseUrls: es el
        // camino que usa un despliegue real en HTTP plano, y ademas el validador de opciones rechaza
        // arrancar si no queda ningun esquema encendido. El puerto ya lo fijo el constructor con
        // UseKestrel, asi que aqui solo se enciende HTTP plano en ese mismo puerto.
        builder.UseSetting("OidcMock:Serving:UseHttps", "false")
            .UseSetting("OidcMock:Serving:AllowHttp", "true")
            .UseSetting("OidcMock:Serving:HttpPort", _address.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("OidcMock:Issuer", Issuer);

        if (_config is not null)
        {
            builder.UseSetting(HostConfigDirectory.ConfigDirectorySettingName, _config.ConfigDirectory);
        }
    }

    /// <summary>
    /// Cliente HTTP real contra el mock. No lleva cookies: el navegador de las pruebas de protocolo
    /// es <see cref="BrowserSession"/>, que si las necesita para la sesion de login.
    /// </summary>
    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false,
        BaseAddress = new Uri(Issuer)
    });
}

/// <summary>
/// Direccion de loopback reservada antes de arrancar nada. Los puertos se reservan porque el
/// <c>redirect_uri</c> del cliente OIDC se escribe en <c>clients.json</c>, y el mock lo lee al
/// arrancar: si el puerto se eligiera despues (Kestrel con puerto 0) la configuracion ya estaria mal.
///
/// Kestrel escucha en <c>AnyIP</c>, asi que el mismo puerto sirve tanto por <c>127.0.0.1</c> como
/// por <c>localhost</c>, que es lo que necesitan las dos.
/// </summary>
public sealed record LoopbackAddress(int Port)
{
    public string BaseAddress => $"http://127.0.0.1:{Port}";

    /// <summary>Variante por nombre de host, para las URLs que el handler OIDC construye.</summary>
    public string LocalhostAddress => $"http://localhost:{Port}";

    public static LoopbackAddress Reserve() => new(FreePort());

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}