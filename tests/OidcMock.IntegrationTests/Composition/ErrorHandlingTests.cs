using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OidcMock.Core.Grants;
using OidcMock.Host;
using OidcMock.IntegrationTests.Endpoints;

namespace OidcMock.IntegrationTests.Composition;

/// <summary>
/// Los errores de protocolo siguen el formato OAuth (RFC 6749 5.2) y los errores no previstos
/// caen en ProblemDetails, para que un fallo del mock no se confunda con un rechazo del servidor real.
/// </summary>
public sealed class ErrorHandlingTests
{
    private const string CrashingPath = "/falla-inesperada";
    private const string TokenPath = "/personafisica/connect/token";

    [Fact]
    public async Task UnErrorNoPrevistoSeRespondeConProblemDetailsYNoConFormatoOAuth()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(AddCrashingEndpoint);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(CrashingPath, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, body.RootElement.GetProperty("status").GetInt32());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
        Assert.False(body.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task ElErrorNoPrevistoNoSeExponeAlCliente()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(AddCrashingEndpoint);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(CrashingPath, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("boom", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnErrorDeProtocoloSeRespondeConElFormatoOAuthYNoConProblemDetails()
    {
        using var client = OidcTestClient.Create();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "grant_type_inventado",
            ["client_id"] = OidcTestClient.ClientId
        });

        using var response = await client.PostAsync(TokenPath, form, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("unsupported_grant_type", body.RootElement.GetProperty("error").GetString());
        Assert.False(body.RootElement.TryGetProperty("traceId", out _));
    }

    /// <summary>
    /// Anade un endpoint que revienta, para provocar un error no previsto de verdad en vez de
    /// simularlo: asi se prueba el pipeline completo, no el manejador aislado.
    /// </summary>
    private static void AddCrashingEndpoint(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services => services.AddSingleton<StartupFilter>());
        builder.Configure(app => { });
    }

    private sealed class StartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    if (context.Request.Path == CrashingPath)
                    {
                        throw new InvalidOperationException("boom: detalle que no debe verse");
                    }

                    await nextMiddleware();
                });

                next(app);
            };
    }
}