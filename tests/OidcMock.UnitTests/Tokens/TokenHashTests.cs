using System.Text;
using OidcMock.Core.Crypto;
using OidcMock.Core.Tokens;

namespace OidcMock.UnitTests.Tokens;

/// <summary>
/// <c>at_hash</c> y <c>c_hash</c> (OpenID Connect Core 3.1.3.6) permiten al <c>id_token</c> atar la
/// identidad a un token concreto, para que un cliente no pueda pegar un id_token de una sesion dentro
/// de otra. El valor es medio hash en base64url, y el algoritmo depende de la firma: con RS256, SHA-256.
///
/// Se comprueba contra el valor calculado por fuera. Una reimplementacion que comprobara contra si misma
/// pasaria aunque dejara de atar nada.
/// </summary>
public sealed class TokenHashTests
{
    private const string AccessToken = "access-token-de-prueba";

    [Fact]
    public void EsLaMitadIzquierdaDelSha256EnBase64Url()
    {
        var digest = System.Security.Cryptography.SHA256.HashData(Encoding.ASCII.GetBytes(AccessToken));
        var expected = Base64Url.Encode(digest.AsSpan(0, digest.Length / 2));

        Assert.Equal(expected, TokenHash.FromAccessToken(AccessToken));
    }

    /// <summary>
    /// Es la mitad izquierda, no el hash entero: asi el hash del token no se puede Used para reconstruir
    /// el token, que es justamente lo que se quiere evitar.
    /// </summary>
    [Fact]
    public void UsaLaMitadDelHashYNotElHashCompleto()
    {
        var digest = System.Security.Cryptography.SHA256.HashData(Encoding.ASCII.GetBytes(AccessToken));
        var expected = Base64Url.Encode(digest.AsSpan(0, digest.Length / 2));

        Assert.NotEqual(Base64Url.Encode(digest), TokenHash.FromAccessToken(AccessToken));
        Assert.Equal(expected.Length, TokenHash.FromAccessToken(AccessToken).Length);
    }

    /// <summary>
    /// El code hash se calcula igual que el del access token: mismo algoritmo, misma longitud. Si
    /// divergieran, el cliente no podria verificar <c>c_hash</c> con el mismo codigo que usa para
    /// <c>at_hash</c>.
    /// </summary>
    [Fact]
    public void ElHashDelCodigoUsaElMismoAlgoritmoQueElDelAccessToken()
    {
        Assert.Equal(
            TokenHash.FromAccessToken(AccessToken).Length,
            TokenHash.FromAuthorizationCode(AccessToken).Length);
    }

    [Fact]
    public void TokensDistintosDanHashesDistintos()
    {
        Assert.NotEqual(TokenHash.FromAccessToken("uno"), TokenHash.FromAccessToken("otro"));
    }

    /// <summary>
    /// base64url no lleva <c>+</c>, <c>/</c> ni <c>=</c>: esos caracteres rompen el token cuando viaja en
    /// la URL o en un encabezado.
    /// </summary>
    [Fact]
    public void ElHashNoContieneCaracteresDeBase64Estandar()
    {
        var hash = TokenHash.FromAccessToken(AccessToken);

        Assert.DoesNotContain('+', hash);
        Assert.DoesNotContain('/', hash);
        Assert.DoesNotContain('=', hash);
    }

    [Fact]
    public void UnValorNuloNoSeConvierteEnUnHash()
    {
        Assert.Throws<ArgumentNullException>(() => TokenHash.FromAccessToken(null!));
    }
}