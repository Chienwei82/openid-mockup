using OidcMock.Core.Configuration;
using OidcMock.Core.Crypto;
using OidcMock.Host.Crypto;

namespace OidcMock.IntegrationTests.Crypto;

public sealed class PemSigningKeyProviderTests
{
    private const string SigningKeyFileName = "signing-key.pem";

    [Fact]
    public void GeneraYPersisteLaClaveCuandoElArchivoNoExiste()
    {
        using var directory = new TempConfigDirectory();

        var key = new PemSigningKeyProvider(directory.Path).GetSigningKey();

        Assert.True(File.Exists(directory.FilePath(SigningKeyFileName)));
        Assert.Equal(SigningKeySizes.KeySizeInBits, key.KeySize);
    }

    [Fact]
    public void RecargaLaClavePersistidaEnLugarDeGenerarUnaNueva()
    {
        using var directory = new TempConfigDirectory();
        var first = new PemSigningKeyProvider(directory.Path).GetSigningKey();

        var second = new PemSigningKeyProvider(directory.Path).GetSigningKey();

        Assert.Equal(first.KeyId, second.KeyId);
    }

    [Fact]
    public void ElKidNoCambiaEntreReiniciosSiElArchivoPemExiste()
    {
        using var directory = new TempConfigDirectory();
        var first = new PemSigningKeyProvider(directory.Path).GetSigningKey();
        var pemBefore = File.ReadAllText(directory.FilePath(SigningKeyFileName));

        var afterRestart = new PemSigningKeyProvider(directory.Path).GetSigningKey();

        Assert.Equal(first.KeyId, afterRestart.KeyId);
        Assert.Equal(pemBefore, File.ReadAllText(directory.FilePath(SigningKeyFileName)));
    }

    [Fact]
    public void FallaConUnErrorClaroSiElPemEstaCorrupto()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteFile(SigningKeyFileName, "no es un pem");

        var exception = Assert.Throws<ConfigurationException>(
            () => new PemSigningKeyProvider(directory.Path).GetSigningKey());

        Assert.Contains(SigningKeyFileName, exception.Message, StringComparison.Ordinal);
    }
}
