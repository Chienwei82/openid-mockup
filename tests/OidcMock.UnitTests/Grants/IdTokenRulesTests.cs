using OidcMock.Core.Grants;

namespace OidcMock.UnitTests.Grants;

/// <summary>
/// La regla que decide si una peticion puede llevar id_token. No tenia pruebas propias, y era la que
/// dos grants (password y el sondeo) no estaban aplicando: emitian una identidad en flujos que nunca
/// pidieron el scope <c>openid</c>.
/// </summary>
public sealed class IdTokenRulesTests
{
    [Theory]
    [InlineData("openid", true)]
    [InlineData("email", false)]
    [InlineData("profile", false)]
    public void SoloOpenidConcedeIdToken(string scope, bool seEmite) =>
        Assert.Equal(seEmite, IdTokenRules.GrantsIdToken([scope]));

    [Fact]
    public void OpenidEntreOtrosScopesTambienConcedeIdToken() =>
        Assert.True(IdTokenRules.GrantsIdToken(["email", "openid", "roles"]));

    [Fact]
    public void SinScopesNoConcedeIdToken() =>
        Assert.False(IdTokenRules.GrantsIdToken([]));

    /// <summary>
    /// La comparacion es exacta: <c>OpenId</c> con otra capitalizacion es un scope distinto, y tratarlo
    /// como el mismo haria que un cliente sin permiso recibiera identidades.
    /// </summary>
    [Theory]
    [InlineData("OpenId")]
    [InlineData("OPENID")]
    [InlineData("openid2")]
    [InlineData("xopenid")]
    public void UnScopeQueSoloSePareceAOpenidNoConcedeIdToken(string scope) =>
        Assert.False(IdTokenRules.GrantsIdToken([scope]));
}