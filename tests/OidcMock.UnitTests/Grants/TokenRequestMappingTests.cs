using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.Core.Scopes;
using OidcMock.UnitTests.Fixtures;

namespace OidcMock.UnitTests.Grants;

/// <summary>
/// Caracterizacion del paso de <see cref="TokenEndpointRequest"/> a <see cref="TokenRequest"/>, el
/// unico punto donde los dos se construyen. Un grant espia captura la peticion y cada test comprueba
/// que un campo de la peticion HTTP cae en su slot y no en el de al lado.
///
/// El motivo es la forma del registro, no su comportamiento: <c>TokenRequest</c> lleva once
/// parametros posicionales, once de ellos <see cref="string"/>, y transponer dos contiguos
/// (<c>userName</c>/<c>password</c>, <c>code</c>/<c>refreshToken</c>) compila sin avisar. Estos tests
/// son la red que hace seguro ese refactor: si un <c>with</c> o una reordenacion los mueve, falla.
/// </summary>
public sealed class TokenRequestMappingTests
{
    private const string Issuer = "https://localhost:5000/personafisica/";
    private const string ClientId = "web-app-spa";

    [Fact]
    public async Task CadaCampoDeLaPeticionLlegaASuSlot()
    {
        var handler = new RecordingGrantHandler();
        var service = ServiceOver(handler);

        await service.IssueTokenAsync(new TokenEndpointRequest(
            new ClientCredentials(ClientAuthenticationMethods.ClientSecretPost, ClientId, null),
            GrantTypes.AuthorizationCode,
            ["openid", "email"],
            Code: "el-codigo",
            RedirectUri: "https://localhost:5173/callback",
            CodeVerifier: "el-verificador",
            RefreshToken: "el-refresh",
            UserName: "jperez",
            Password: "clave",
            DeviceCode: "el-device-code"), Issuer);

        var captured = handler.Captured;

        Assert.Equal(Issuer, captured.Issuer);
        Assert.Equal(ClientId, captured.Client.ClientId);
        Assert.Equal(["openid", "email"], captured.Scopes);
        Assert.Equal("el-codigo", captured.Code);
        Assert.Equal("https://localhost:5173/callback", captured.RedirectUri);
        Assert.Equal("el-verificador", captured.CodeVerifier);
        Assert.Equal("el-refresh", captured.RefreshToken);
        Assert.Equal("jperez", captured.UserName);
        Assert.Equal("clave", captured.Password);
        Assert.Equal("el-device-code", captured.DeviceCode);
    }

    /// <summary>
    /// <c>ScopesRequested</c> vale lo que el cliente **pidio**, no lo que el mock concedio. Sin
    /// scopes en la peticion la marca es false: el grant refresh_token usa esa distincion para el
    /// narrowing, y darla por cierta haria que una peticion sin scope invalidara los scopes
    /// concedidos en lugar de conservarlos. Los scopes del registro, en cambio, ya vienen
    /// normalizados a la minima de OIDC.
    /// </summary>
    [Fact]
    public async Task SinScopesSolicitadosLaMarcaEsFalsaYLosScopesVienenNormalizados()
    {
        var handler = new RecordingGrantHandler();

        await ServiceOver(handler).IssueTokenAsync(
            new TokenEndpointRequest(
                new ClientCredentials(ClientAuthenticationMethods.ClientSecretPost, ClientId, null),
                GrantTypes.RefreshToken,
                [],
                Code: null,
                RedirectUri: null,
                CodeVerifier: null,
                RefreshToken: "el-refresh",
                UserName: null,
                Password: null),
            Issuer);

        Assert.False(handler.Captured.ScopesRequested);
        Assert.Equal([ScopeNames.OpenId], handler.Captured.Scopes);
    }

    /// <summary>
    /// Con scopes en la peticion la marca es cierta, que es lo que habilita el narrowing del grant
    /// refresh_token: ampliar lo concedido seria invalid_scope.
    /// </summary>
    [Fact]
    public async Task ConScopesSolicitadosLaMarcaEsCierta()
    {
        var handler = new RecordingGrantHandler();

        await ServiceOver(handler).IssueTokenAsync(
            new TokenEndpointRequest(
                new ClientCredentials(ClientAuthenticationMethods.ClientSecretPost, ClientId, null),
                GrantTypes.RefreshToken,
                ["openid"],
                Code: null,
                RedirectUri: null,
                CodeVerifier: null,
                RefreshToken: "el-refresh",
                UserName: null,
                Password: null),
            Issuer);

        Assert.True(handler.Captured.ScopesRequested);
    }

    /// <summary>
    /// El espia se registra una vez por cada grant que exercise la caracterizacion, porque el
    /// registro se indexa por nombre de grant y solo admite un handler por nombre.
    /// </summary>
    private static TokenEndpointService ServiceOver(RecordingGrantHandler handler) =>
        new(
            ScopeStoreFixture.Create(),
            ClientStoreFixture.AuthenticatorOver(ClientStoreFixture.Create()),
            new GrantHandlerRegistry(
            [
                new GrantOver(GrantTypes.AuthorizationCode, handler),
                new GrantOver(GrantTypes.RefreshToken, handler)
            ]));

    /// <summary>
    /// Envoltorio que registra el mismo espia bajo dos nombres de grant: el registro se indexa por
    /// <see cref="IGrantHandler.GrantType"/> y solo admite un handler por nombre, asi que un espia con
    /// un unico nombre fijo solo capturaria uno de los dos grants que exercise la caracterizacion.
    /// </summary>
    private sealed class GrantOver(string grantType, RecordingGrantHandler recorder) : IGrantHandler
    {
        public string GrantType => grantType;

        public bool IssuesIdToken => false;

        public Task<Result<TokenResponse>> HandleAsync(TokenRequest request) => recorder.HandleAsync(request);
    }

    /// <summary>No emite nada: solo deja constancia de la peticion que recibio.</summary>
    private sealed class RecordingGrantHandler
    {

        public TokenRequest Captured { get; private set; } = null!;

        public Task<Result<TokenResponse>> HandleAsync(TokenRequest request)
        {
            Captured = request;

            return Task.FromResult(Result<TokenResponse>.Fail(
                ProtocolErrors.InvalidRequest("Grabacion: no se emite nada.")));
        }
    }
}
