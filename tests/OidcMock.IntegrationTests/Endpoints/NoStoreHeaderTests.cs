using System.Net;
using OidcMock.Core.Discovery;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Ninguna respuesta que lleve una credencial o datos de usuario puede quedar en una cache: sin
/// <c>no-store</c>, un proxy o el navegador guardan el access token, el estado de un token ajeno o los
/// claims de una persona. RFC 6749 5.1 lo exige para el token endpoint; el resto devuelve material igual
/// de sensible y se le aplica la misma regla.
///
/// El <c>Pragma: no-cache</c> es el equivalente para caches HTTP/1.0.
/// </summary>
public sealed class NoStoreHeaderTests
{
    /// <summary>
    /// Endpoints cuya respuesta es una credencial, el estado de una credencial o datos de persona. La
    /// lista esta a proposito en el test: anadir un endpoint obliga a decidir si lleva <c>no-store</c>,
    /// en vez de que se pierda por olvido.
    /// </summary>
    public static TheoryData<string> CredentialBearingEndpoints() =>
    [
        EndpointPaths.Token,
        EndpointPaths.DeviceAuthorization,
        EndpointPaths.Ciba,
        EndpointPaths.PushedAuthorizationRequest,
        EndpointPaths.Introspection,
        EndpointPaths.Revocation,
    ];

    /// <summary>
    /// Se piden sin autenticar a proposito: la cabecera tiene que ponerla el endpoint aunque la
    /// peticion vaya a fallar, porque un 401 con el secreto de otro tambien es informacion que no debe
    /// quedar cacheada.
    /// </summary>
    [Theory]
    [MemberData(nameof(CredentialBearingEndpoints))]
    public async Task LosEndpointsQueDevuelvenCredencialesNoSeCachean(string path)
    {
        using var client = Create();

        using var response = await client.PostAsync(
            $"{PathBase}/{path}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = ServiceClientId }),
            TestContext.Current.CancellationToken);

        AssertNoStore(response);
    }

    /// <summary>
    /// El userinfo devuelve los claims de la persona, y tambien sin token: el error dice si el token
    /// existia, asi que va sin cachear igual.
    /// </summary>
    [Fact]
    public async Task ElUserinfoNoSeCachea()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{PathBase}/{EndpointPaths.UserInfo}",
            TestContext.Current.CancellationToken);

        AssertNoStore(response);
    }

    private static void AssertNoStore(HttpResponseMessage response)
    {
        var cacheControl = response.Headers.CacheControl;

        Assert.NotNull(cacheControl);
        Assert.True(cacheControl!.NoStore, $"Cache-Control: {cacheControl}");
        Assert.True(cacheControl.NoCache, $"Cache-Control: {cacheControl}");
        Assert.Contains("no-cache", response.Headers.Pragma.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
