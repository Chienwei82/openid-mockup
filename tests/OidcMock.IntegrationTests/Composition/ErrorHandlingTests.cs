using OidcMock.IntegrationTests;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Host;
using OidcMock.IntegrationTests.Endpoints;

namespace OidcMock.IntegrationTests.Composition;

/// <summary>
/// Los errores de protocolo siguen el formato OAuth (RFC 6749 5.2) y los errores no previstos
/// caen en ProblemDetails, para que un fallo del mock no se confunda con un rechazo del servidor real.
/// </summary>
public sealed class ErrorHandlingTests
{
    private const string JwksPath = "/personafisica/.well-known/openid-configuration/jwks";
    private const string TokenPath = "/personafisica/connect/token";
    private const string CorruptKey = "no soy una clave PEM";

    [Fact]
    public async Task UnErrorNoPrevistoSeRespondeConProblemDetailsYNoConFormatoOAuth()
    {
        using var factory = FactoryWithCorruptSigningKey();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(JwksPath, TestContext.Current.CancellationToken);
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
        using var factory = FactoryWithCorruptSigningKey();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(JwksPath, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(CorruptKey, body, StringComparison.OrdinalIgnoreCase);
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
    /// La clave de firma ilegible hace que el endpoint del JWKS reviente de verdad, dentro del
    /// pipeline real y no en un endpoint de laboratorio.
    /// </summary>
    private static WebApplicationFactory<Program> FactoryWithCorruptSigningKey()
    {
        var directory = new TempConfigDirectory();
        directory.CopyRepositoryConfiguration();
        directory.WriteFile(ConfigurationFiles.SigningKey, CorruptKey);

        return MockHost.Create()
            .WithWebHostBuilder(builder => builder.UseSetting(
                HostConfigDirectory.ConfigDirectorySettingName,
                directory.Path));
    }
}