using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OidcMock.ClientCompatibilityTests.Infrastructure;

namespace OidcMock.ClientCompatibilityTests;

/// <summary>
/// El camino de maquina a maquina: un cliente confidencial pide un access token con
/// <c>client_credentials</c> y lo presenta en una API protegida con JwtBearer.
///
/// La API no recibe ni la clave publica ni el emisor a mano: los saca del discovery del mock, igual
/// que en produccion. Si el <c>aud</c> del token no fuera el que espera, o si la firma no validara
/// contra el JWKS, el JwtBearer lo rechazaria y estas pruebas lo verian.
/// </summary>
public sealed class MachineToMachineTests
{
    [Fact]
    public async Task ElTokenDeClientCredentialsSirveEnUnaApiProtegida()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        var api = await world.StartApiAsync();

        var accessToken = await RequestAccessTokenAsync(world);

        using var client = new HttpClient { BaseAddress = new Uri(api.BaseAddress) };
        using var request = new HttpRequestMessage(HttpMethod.Get, JwtBearerApiHost.ProtectedPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Sin token no hay paso: la API tiene que responder 401 y no devolver el recurso.
    /// </summary>
    [Fact]
    public async Task LaApiProtegidaRechazaLasPeticionesSinToken()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        var api = await world.StartApiAsync();

        using var client = new HttpClient { BaseAddress = new Uri(api.BaseAddress) };
        using var response = await client.GetAsync(JwtBearerApiHost.ProtectedPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Un token con la firma manipulada tiene que rechazarlo: es la comprobacion que hace el
    /// JwtBearer contra el JWKS del discovery, y es la que un mock con una clave trivial no detectaria.
    /// </summary>
    [Fact]
    public async Task LaApiProtegidaRechazaUnTokenAlterado()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        var api = await world.StartApiAsync();

        var accessToken = await RequestAccessTokenAsync(world);

        using var client = new HttpClient { BaseAddress = new Uri(api.BaseAddress) };
        using var request = new HttpRequestMessage(HttpMethod.Get, JwtBearerApiHost.ProtectedPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Tamper(accessToken));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Pide el token al token endpoint descubierto, con las credenciales en el cuerpo.</summary>
    private static async Task<string> RequestAccessTokenAsync(CompatibilityWorld world)
    {
        var configuration = await world.ReadConfigurationAsync();

        using var client = world.Mock.CreateApiClient();
        using var response = await client.PostAsync(
            configuration.TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = CompatibilityConfig.ServiceClientId,
                ["client_secret"] = CompatibilityConfig.ServiceClientSecret,
                ["scope"] = "openid email"
            }),
            TestContext.Current.CancellationToken);

        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(payload).RootElement.GetProperty("access_token").GetString()!;
    }

    /// <summary>
    /// Cambia un caracter de la firma. El token sigue siendo un JWT bien formado y el payload sigue
    /// siendo legible: lo unico que cambia es que la firma ya no cuadra con el JWKS.
    /// </summary>
    private static string Tamper(string accessToken)
    {
        var segments = accessToken.Split('.');
        var signature = segments[2].ToCharArray();

        signature[0] = signature[0] == 'A' ? 'B' : 'A';

        return $"{segments[0]}.{segments[1]}.{new string(signature)}";
    }
}
