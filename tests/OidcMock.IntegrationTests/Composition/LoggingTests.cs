using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OidcMock.Core.Grants;
using OidcMock.IntegrationTests.Endpoints;

namespace OidcMock.IntegrationTests.Composition;

/// <summary>
/// El log del mock dice que paso, sin volcar credenciales: los tokens, el refresh token y el secreto
/// del cliente no se registran nunca, aunque la operacion haya ocurrido.
/// </summary>
public sealed class LoggingTests
{
    /// <summary>
    /// Regresion: IdentityModel cachea el proveedor de firma por kid, de modo que dos instancias que
    /// comparten clave (dos pruebas, dos WebApplicationFactory) se pisan la RSA. Al liberar la
    /// primera, la segunda falla con ObjectDisposedException al firmar. Aqui se fuerza ese cruce:
    /// dos navegadores independientes sobre el mismo host, que es como se ve en la practica.
    /// </summary>
    [Fact]
    public async Task FirmaTokensEnDosNavegadoresDistintos()
    {
        using var first = OidcTestClient.Create();
        using var second = OidcTestClient.Create();

        var firstTokens = await OidcTestClient.RequestTokensAsync(
            first,
            GrantTypes.AuthorizationCode,
            await OidcTestClient.SignInAsync(first));

        var secondTokens = await OidcTestClient.RequestTokensAsync(
            second,
            GrantTypes.AuthorizationCode,
            await OidcTestClient.SignInAsync(second));

        Assert.NotNull(firstTokens.AccessToken);
        Assert.NotNull(secondTokens.AccessToken);

        // Y que no sean el mismo token: si el segundo navegador recibiera el del primero, la prueba
        // pasaria sin haber firmado nada con la segunda RSA.
        Assert.NotEqual(firstTokens.AccessToken, secondTokens.AccessToken);
    }

    [Fact]
    public async Task EmiteUnEventoAlEmitirTokensConSuClienteYSuCaducidad()
    {
        using var harness = new LogHarness();
        using var client = harness.CreateClient();

        await OidcTestClient.RequestTokensAsync(
            client,
            GrantTypes.ClientCredentials,
            extra: new Dictionary<string, string>
            {
                ["client_id"] = OidcTestClient.ServiceClientId,
                ["client_secret"] = OidcTestClient.ServiceSecret
            });

        var entry = Assert.Single(harness.Recorder.In(TokenEndpointsLogCategory), entry => entry.EventId == 3000);

        Assert.Contains(OidcTestClient.ServiceClientId, entry.Message, StringComparison.Ordinal);
        Assert.Contains("client_credentials", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoRegistraElAccessTokenNiElIdTokenEmitidos()
    {
        using var harness = new LogHarness();
        using var client = harness.CreateClient();

        var code = await OidcTestClient.SignInAsync(client);
        var tokens = await OidcTestClient.RequestTokensAsync(client, GrantTypes.AuthorizationCode, code);

        // El guard va primero: sin el, "no aparece" tambien se cumple con un log vacio.
        harness.Recorder.AssertLoggedSomething();

        Assert.NotNull(tokens.AccessToken);
        Assert.NotNull(tokens.IdToken);
        Assert.False(harness.Recorder.OwnLogsMention(tokens.AccessToken!));
        Assert.False(harness.Recorder.OwnLogsMention(tokens.IdToken!));
    }

    [Fact]
    public async Task NoRegistraElRefreshTokenNiElCodigoDeAutorizacion()
    {
        using var harness = new LogHarness();
        using var client = harness.CreateClient();

        var code = await OidcTestClient.SignInAsync(client);
        var tokens = await OidcTestClient.RequestTokensAsync(client, GrantTypes.AuthorizationCode, code);

        harness.Recorder.AssertLoggedSomething();

        Assert.NotNull(tokens.RefreshToken);
        Assert.False(harness.Recorder.OwnLogsMention(tokens.RefreshToken!));
        Assert.False(harness.Recorder.OwnLogsMention(code));

        // Y el log tiene que seguir siendo util: si no dice ni el cliente ni el grant, no sirve para
        // diagnosticar nada.
        Assert.Contains(
            harness.Recorder.In(TokenEndpointsLogCategory),
            entry => entry.EventId == 3000
                && entry.Message.Contains(OidcTestClient.ClientId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoRegistraElSecretoDelClienteCuandoLaAutenticacionFalla()
    {
        using var harness = new LogHarness();
        using var client = harness.CreateClient();
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = GrantTypes.ClientCredentials,
            ["client_id"] = OidcTestClient.ServiceClientId,
            ["client_secret"] = "secreto-inventado"
        };

        using var response = await OidcTestClient.PostTokenAsync(client, form);

        Assert.False(harness.Recorder.OwnLogsMention("secreto-inventado"));
        Assert.Contains(harness.Recorder.Entries, entry => entry.EventId is 3101 or 3001);
        harness.Recorder.AssertLoggedSomething();
    }

    [Fact]
    public async Task AnunciaLaConfiguracionCargadaAlArrancar()
    {
        using var harness = new LogHarness();
        using var client = harness.CreateClient();

        using var response = await client.GetAsync(
            "/personafisica/.well-known/openid-configuration",
            TestContext.Current.CancellationToken);

        Assert.Contains(harness.Recorder.Entries, entry => entry.EventId == 1000);
    }

    /// <summary>
    /// Categoria bajo la que el token endpoint publica sus eventos.
    /// </summary>
    private const string TokenEndpointsLogCategory = "OidcMock.Host.Endpoints.TokenEndpoints";

    /// <summary>
    /// Host de pruebas con un provider de log en memoria.
    /// </summary>
    private sealed class LogHarness : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;

        public LogHarness()
        {
            Recorder = new RecordingLoggerProvider();

            // Se usa MockHost y no un factory propio para que el host no levante un watcher extra:
            // el limite de inotify del sistema se agota antes de que estas pruebas empiecen.
            _factory = MockHost.Create().WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
                logging.AddProvider(Recorder).SetMinimumLevel(LogLevel.Debug)));
        }

        public RecordingLoggerProvider Recorder { get; }

        // AllowAutoRedirect desactivado: el authorize responde con un 302 al cliente y el codigo se lee del
        // Location, igual que en las pruebas de endpoints.
        public HttpClient CreateClient() =>
            _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        public void Dispose() => _factory.Dispose();
    }
}