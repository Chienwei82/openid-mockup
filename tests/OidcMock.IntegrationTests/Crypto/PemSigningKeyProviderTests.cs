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

    /// <summary>
    /// Montar el config de solo lectura es normal (Docker con usuario no-root, un config en un
    /// volumen compartido). El mock debe servir igualmente: solo pierde la persistencia del kid.
    /// </summary>
    [Fact]
    public void SirveIgualCuandoNoPuedePersistirLaClavePorFaltaDePermisos()
    {
        // Los permisos POSIX son lo que reproduce el caso del contenedor; en Windows el helper no
        // puede montar un directorio de solo lectura y el escenario no existe.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var readOnly = new ReadOnlyDirectory();
        using var provider = new PemSigningKeyProvider(readOnly.Path);

        var key = provider.GetSigningKey();

        Assert.Equal(SigningKeySizes.KeySizeInBits, key.KeySize);
        Assert.NotEmpty(key.KeyId);
    }

    /// <summary>
    /// Copia los archivos a un directorio sin permiso de escritura, como el config montado de solo
    /// lectura. El error real aqui es UnauthorizedAccessException al no poder crear el PEM.
    /// </summary>
    private sealed class ReadOnlyDirectory : IDisposable
    {
        public ReadOnlyDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"oidc-mock-solo-lectura-{Guid.NewGuid():N}");

            Directory.CreateDirectory(Path);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            }
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
