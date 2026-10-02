using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OidcMock.ClientCompatibilityTests.Infrastructure;

namespace OidcMock.ClientCompatibilityTests;

/// <summary>
/// El cliente no se configura con las rutas del mock: solo con su issuer. Todo lo demas (token
/// endpoint, JWKS, end session) llega por metadata, igual que en una aplicacion real.
///
/// Estas pruebas comprueban el punto de partida del que dependen todas las otras: que el documento
/// que anuncia el issuer sea utilizable tal cual.
/// </summary>
public sealed class DiscoveryTests
{
    [Fact]
    public async Task ElClienteDescubreLaConfiguracionPorMetadata()
    {
        await using var world = await CompatibilityWorld.StartAsync();

        var configuration = await world.ReadConfigurationAsync();

        Assert.Equal(world.Mock.Issuer, configuration.Issuer);
        Assert.Equal($"{world.Mock.Issuer.TrimEnd('/')}/{MockEndpoints.Authorize}", configuration.AuthorizationEndpoint);
        Assert.Equal($"{world.Mock.Issuer.TrimEnd('/')}/{MockEndpoints.Token}", configuration.TokenEndpoint);
        Assert.Equal($"{world.Mock.Issuer.TrimEnd('/')}/{MockEndpoints.EndSession}", configuration.EndSessionEndpoint);
    }

    [Fact]
    public async Task ElIssuerSeAnunciaConBarraFinal()
    {
        await using var world = await CompatibilityWorld.StartAsync();

        var issuer = (await world.ReadDiscoveryDocumentAsync()).GetProperty("issuer").GetString();

        // El emisor se compara con el del token (OIDC Core 3.1.3.7) y es el Authority del cliente:
        // sin barra final, un cliente real rechazaria el id_token al canjear el codigo.
        Assert.NotNull(issuer);
        Assert.EndsWith("/", issuer, StringComparison.Ordinal);
        Assert.Equal(world.Mock.Issuer, issuer);
    }

    [Fact]
    public async Task ElJwksPublicadoPermiteValidarLaFirmaDeLosTokens()
    {
        await using var world = await CompatibilityWorld.StartAsync();

        var configuration = await world.ReadConfigurationAsync();
        var document = await world.ReadDiscoveryDocumentAsync();

        // Las claves llegan por el jwks_uri del discovery y tienen que servir para el RS256 que el
        // mock anuncia: si no, el handler OIDC rechaza el id_token al canjear el codigo.
        Assert.Contains(
            "RS256",
            document.GetProperty("id_token_signing_alg_values_supported").EnumerateArray().Select(alg => alg.GetString()));
        Assert.NotEmpty(configuration.SigningKeys);

        // IdentityModel convierte cada clave del JWKS en una RsaSecurityKey con su kid, que es lo
        // que despues casa con la cabecera del id_token.
        Assert.All(
            configuration.SigningKeys,
            key => Assert.False(string.IsNullOrEmpty(Assert.IsType<RsaSecurityKey>(key).KeyId)));
    }

    [Fact]
    public async Task ElClienteNoNecesitaConfiguracionAdicionalParaOperar()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        await world.StartClientAsync();

        var options = ClientOptions(world);

        // Configurado solo con el issuer, el handler OIDC resuelve el token endpoint por su cuenta.
        Assert.Equal(world.Mock.Issuer, options.Authority);
        Assert.Equal(OpenIdConnectResponseType.Code, options.ResponseType);
        Assert.Equal(
            $"{world.Mock.Issuer.TrimEnd('/')}/{MockEndpoints.Discovery}",
            Assert.IsType<ConfigurationManager<OpenIdConnectConfiguration>>(options.ConfigurationManager).MetadataAddress);
    }

    /// <summary>
    /// Opciones ya resueltas del cliente. Resolverlas aqui dispara el post-configure del handler, que
    /// es donde se construye el ConfigurationManager a partir del Authority.
    /// </summary>
    internal static OpenIdConnectOptions ClientOptions(CompatibilityWorld world) =>
        world.ClientServices
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
}