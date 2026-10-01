using OidcMock.Core.Codes;
using OidcMock.Core.Crypto;
using OidcMock.Core.Errors;

namespace OidcMock.UnitTests.Codes;

public sealed class PkceCodeChallengeTests
{
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    [Fact]
    public void VerificaElDesafioPlainComparandoElVerificadorTalCual()
    {
        var challenge = CodeChallenges.Create(PkceCodeChallengeMethods.Plain, Verifier);

        Assert.True(CodeChallenges.Matches(challenge, PkceCodeChallengeMethods.Plain, Verifier));
    }

    [Fact]
    public void PlainRechazaUnVerificadorDistinto()
    {
        var challenge = CodeChallenges.Create(PkceCodeChallengeMethods.Plain, Verifier);

        Assert.False(CodeChallenges.Matches(challenge, PkceCodeChallengeMethods.Plain, "otro-verificador"));
    }

    [Fact]
    public void VerificaElDesafioS256ConElSha256DelVerificador()
    {
        var challenge = CodeChallenges.Create(PkceCodeChallengeMethods.Sha256, Verifier);

        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge);
        Assert.True(CodeChallenges.Matches(challenge, PkceCodeChallengeMethods.Sha256, Verifier));
    }

    [Fact]
    public void S256RechazaUnVerificadorDistinto()
    {
        var challenge = CodeChallenges.Create(PkceCodeChallengeMethods.Sha256, Verifier);

        Assert.False(CodeChallenges.Matches(challenge, PkceCodeChallengeMethods.Sha256, "otro-verificador"));
    }

    [Fact]
    public void ElDesafioSeComparaSinFiltrarDiferenciasDeLongitudNiMayusculas()
    {
        var challenge = CodeChallenges.Create(PkceCodeChallengeMethods.Sha256, Verifier);

        Assert.False(CodeChallenges.Matches(challenge, PkceCodeChallengeMethods.Sha256, Verifier + "x"));
    }

    [Fact]
    public void UnMetodoDesconocidoNoSeConsideraValido()
    {
        Assert.False(CodeChallenges.Matches("cualquiera", "MD5", Verifier));
    }

    [Fact]
    public void UnDesafioVacioNoEsValido()
    {
        Assert.False(CodeChallenges.Matches(string.Empty, PkceCodeChallengeMethods.Plain, Verifier));
    }
}