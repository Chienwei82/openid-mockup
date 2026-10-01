using System.Text.Json;
using OidcMock.Core.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Lee y deserializa un archivo JSON de configuracion, cacheando el resultado ya proyectado a
/// dominio y opcionalmente releyendolo cuando el archivo cambia en disco (reloadOnChange).
/// </summary>
internal sealed class JsonFileLoader<TFile, TDomain> where TFile : class
{
    private readonly string _filePath;
    private readonly string _fileName;
    private readonly bool _reloadOnChange;
    private readonly Func<TFile, TDomain> _toDomain;
    private readonly Lock _gate = new();

    private TDomain? _cached;
    private FileStamp _cachedStamp;

    public JsonFileLoader(
        string configDirectory,
        string fileName,
        bool reloadOnChange,
        Func<TFile, TDomain> toDomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(toDomain);

        _filePath = Path.Combine(configDirectory, fileName);
        _fileName = fileName;
        _reloadOnChange = reloadOnChange;
        _toDomain = toDomain;
    }

    public TDomain Load()
    {
        lock (_gate)
        {
            if (_cached is not null && !_reloadOnChange)
            {
                return _cached;
            }

            var currentStamp = ReadStamp();

            if (_cached is not null && currentStamp == _cachedStamp)
            {
                return _cached;
            }

            _cached = _toDomain(Read());
            _cachedStamp = currentStamp;
            return _cached;
        }
    }

    private FileStamp ReadStamp()
    {
        var fileInfo = new FileInfo(_filePath);
        if (!fileInfo.Exists)
        {
            throw new ConfigurationException(_fileName, "el archivo no existe en el directorio de configuracion.");
        }

        return new FileStamp(fileInfo.LastWriteTimeUtc, fileInfo.Length);
    }

    private TFile Read()
    {
        var json = File.ReadAllText(_filePath);

        try
        {
            return JsonSerializer.Deserialize<TFile>(json, JsonConfiguration.SerializerOptions)
                ?? throw new ConfigurationException(_fileName, "el archivo esta vacio o no contiene el objeto esperado.");
        }
        catch (JsonException exception)
        {
            throw new ConfigurationException(_fileName, $"JSON mal formado: {exception.Message}", exception);
        }
        catch (NotSupportedException exception)
        {
            throw new ConfigurationException(_fileName, $"formato no soportado: {exception.Message}", exception);
        }
    }

    private readonly record struct FileStamp(DateTime LastWriteTimeUtc, long Length);
}
