using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Revocation;

namespace OidcMock.UnitTests.Revocation;

/// <summary>
/// El store de revocaciones es la memoria que hace que revocar un access token (un JWT sin estado)
/// sirva de algo: sin el, /userinfo y /introspect seguirian aceptandolo hasta que caducara solo.
/// </summary>
public sealed class InMemoryTokenRevocationStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);

    [Fact]
    public void UnAccessTokenSinRevocarNoEstaRevocado()
    {
        Assert.False(Store().IsAccessTokenRevoked("jti-1"));
    }

    [Fact]
    public void RevocarUnAccessTokenLoDejaMarcado()
    {
        var store = Store();

        store.RevokeAccessToken("jti-1", Now.AddMinutes(30));

        Assert.True(store.IsAccessTokenRevoked("jti-1"));
    }

    [Fact]
    public void LaRevocacionDeUnAccessTokenNoAfectaAOtro()
    {
        var store = Store();
        store.RevokeAccessToken("jti-1", Now.AddMinutes(30));

        Assert.False(store.IsAccessTokenRevoked("jti-2"));
    }

    /// <summary>
    /// Una revocacion no puede caducar antes que el token: si el exp todavia no ha llegado, el jti
    /// sigue en la lista aunque el reloj avance, porque el token sigue siendo valido.
    /// </summary>
    [Fact]
    public void LaRevocacionDelAccessTokenCaducaConSuPropioExp()
    {
        var store = Store();
        store.RevokeAccessToken("jti-1", Now.AddMinutes(30));

        _clock.Advance(TimeSpan.FromMinutes(29));
        store.Expire();

        Assert.True(store.IsAccessTokenRevoked("jti-1"));
    }

    [Fact]
    public void ExpiradaLaVigenciaDelTokenLaRevocacionSeDescarta()
    {
        var store = Store();
        store.RevokeAccessToken("jti-1", Now.AddMinutes(30));

        _clock.Advance(TimeSpan.FromMinutes(31));
        store.Expire();

        Assert.False(store.IsAccessTokenRevoked("jti-1"));
    }

    [Fact]
    public void UnaFamiliaSinRevocarNoEstaRevocada()
    {
        Assert.False(Store().IsRefreshTokenFamilyRevoked("fam-1"));
    }

    [Fact]
    public void RevocarUnaFamiliaLaDejaMarcada()
    {
        var store = Store();

        store.RevokeRefreshTokenFamily("fam-1", Now.AddHours(8));

        Assert.True(store.IsRefreshTokenFamilyRevoked("fam-1"));
    }

    [Fact]
    public void LaRevocacionDeUnaFamiliaNoAfectaAOtra()
    {
        var store = Store();
        store.RevokeRefreshTokenFamily("fam-1", Now.AddHours(8));

        Assert.False(store.IsRefreshTokenFamilyRevoked("fam-2"));
    }

    [Fact]
    public void ExpiradaLaFamiliaSuRevocacionSeDescarta()
    {
        var store = Store();
        store.RevokeRefreshTokenFamily("fam-1", Now.AddHours(8));

        _clock.Advance(TimeSpan.FromHours(9));
        store.Expire();

        Assert.False(store.IsRefreshTokenFamilyRevoked("fam-1"));
    }

    [Fact]
    public void RevocarVariasVecesElMismoTokenNoDuplicaEntradas()
    {
        var store = Store();

        store.RevokeAccessToken("jti-1", Now.AddMinutes(30));
        store.RevokeAccessToken("jti-1", Now.AddMinutes(30));

        Assert.True(store.IsAccessTokenRevoked("jti-1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void RevocarUnIdentificadorVacioNoRegistraNada(string tokenId)
    {
        var store = Store();

        store.RevokeAccessToken(tokenId, Now.AddMinutes(30));
        store.RevokeRefreshTokenFamily(tokenId, Now.AddHours(8));

        Assert.False(store.IsAccessTokenRevoked(tokenId));
        Assert.False(store.IsRefreshTokenFamilyRevoked(tokenId));
    }

    private InMemoryTokenRevocationStore Store() => new(_clock);
}