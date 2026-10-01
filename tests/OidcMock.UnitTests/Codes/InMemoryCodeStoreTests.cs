using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Codes;
using OidcMock.Core.Crypto;
using OidcMock.Core.Errors;

namespace OidcMock.UnitTests.Codes;

public sealed class InMemoryCodeStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly InMemoryCodeStore _store;

    public InMemoryCodeStoreTests() => _store = new InMemoryCodeStore(_clock);

    [Fact]
    public void EmiteUnCodigoQueSePuedeCanjear()
    {
        var issued = Issue();

        var redeemed = _store.Redeem(issued.Code);

        Assert.True(redeemed.Succeeded);
        Assert.Equal(issued.Code, redeemed.Value?.Code);
    }

    [Fact]
    public void ElCodigoEsDeUnSoloUso()
    {
        var issued = Issue();

        Assert.True(_store.Redeem(issued.Code).Succeeded);
        Assert.False(_store.Redeem(issued.Code).Succeeded);
    }

    [Fact]
    public void UnCodigoCaducadoNoSePuedeCanjear()
    {
        var issued = Issue();

        _clock.Advance(CodeLifetime + TimeSpan.FromSeconds(1));

        var redeemed = _store.Redeem(issued.Code);

        Assert.False(redeemed.Succeeded);
        Assert.Equal("invalid_grant", redeemed.Error?.Code);
    }

    [Fact]
    public void ElCodigoSigueValidoJustoAntesDeCaducar()
    {
        var issued = Issue();

        _clock.Advance(CodeLifetime - TimeSpan.FromSeconds(1));

        Assert.True(_store.Redeem(issued.Code).Succeeded);
    }

    [Fact]
    public void UnCodigoDesconocidoNoSePuedeCanjear()
    {
        var redeemed = _store.Redeem("codigo-inventado");

        Assert.False(redeemed.Succeeded);
        Assert.Equal("invalid_grant", redeemed.Error?.Code);
    }

    [Fact]
    public void ElCodigoGuardaLosDatosDeLaPeticionDeAutorizacion()
    {
        var issued = Issue(nonce: "n-abc", state: "st-123", scopes: ["openid", "email"]);

        var redeemed = _store.Redeem(issued.Code).ValueOrThrow();

        Assert.Equal("web-app-spa", redeemed.ClientId);
        Assert.Equal("jperez", redeemed.UserName);
        Assert.Equal("n-abc", redeemed.Nonce);
        Assert.Equal("st-123", redeemed.State);
        Assert.Equal(["openid", "email"], redeemed.Scopes);
        Assert.Equal(Now.ToUnixTimeSeconds(), redeemed.AuthenticatedAt.ToUnixTimeSeconds());
    }

    [Fact]
    public void ElCodigoGuardaElDesafioPkceParaVerificarloAlCanjearlo()
    {
        var challenge = Sha256Base64Url(CodeVerifier);
        var issued = Issue(codeChallenge: challenge, codeChallengeMethod: S256);

        var redeemed = _store.Redeem(issued.Code).ValueOrThrow();

        Assert.Equal(S256, redeemed.CodeChallengeMethod);
        Assert.Equal(challenge, redeemed.CodeChallenge);
    }

    [Fact]
    public void LosCodigosCaducadosNoSeAcumulanEnElStore()
    {
        Issue();

        _clock.Advance(CodeLifetime + TimeSpan.FromSeconds(1));

        Assert.Empty(_store.List());
    }

    [Fact]
    public void ExpireNoLiberaLosCodigosQueSiguenVigentes()
    {
        var issued = Issue();

        _store.Expire();

        Assert.Single(_store.List());
        Assert.True(_store.Redeem(issued.Code).Succeeded);
    }

    [Fact]
    public void ExpireLiberaLosCodigosCaducadosSinInvalidarLosVivos()
    {
        var expired = Issue();
        _clock.Advance(CodeLifetime + TimeSpan.FromSeconds(1));
        var alive = Issue();

        _store.Expire();

        Assert.Equal([alive.Code], _store.List().Select(code => code.Code));
        Assert.False(_store.Redeem(expired.Code).Succeeded);
        Assert.True(_store.Redeem(alive.Code).Succeeded);
    }

    [Fact]
    public void CanjearUnCodigoLoEliminaDelStore()
    {
        var issued = Issue();

        _store.Redeem(issued.Code);

        Assert.Empty(_store.List());
    }

    [Fact]
    public void DosCodigosEmitidosNoSeConfunden()
    {
        var first = Issue();
        var second = Issue();

        Assert.NotEqual(first.Code, second.Code);
        Assert.True(_store.Redeem(first.Code).Succeeded);
        Assert.True(_store.Redeem(second.Code).Succeeded);
    }

    private const string S256 = "S256";
    private const string CodeVerifier = "verificador-de-43-caracteres-base64url-XXXXX";

    private AuthorizationCode Issue(
        string? nonce = null,
        string? state = null,
        IReadOnlyList<string>? scopes = null,
        string? codeChallenge = null,
        string? codeChallengeMethod = null) =>
        _store.Issue(new AuthorizationCodeRequest(
            "web-app-spa",
            "jperez",
            scopes ?? ["openid"],
            nonce,
            state,
            codeChallenge,
            codeChallengeMethod,
            CodeLifetime));

    private static string Sha256Base64Url(string verifier) =>
        Base64Url.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
}