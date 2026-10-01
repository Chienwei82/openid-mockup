using System.Text.Json;
using OidcMock.Core.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Lee y deserializa un archivo JSON de configuracion, cacheando el resultado y opcionalmente
/// releyendolo cuando el archivo cambia en disco (reloadOnChange).
/// </summary>
internal sealed class JsonFileLoader<T> where T : class
{
    private readonly string _filePath;
    private readonly string _fileName;
    private readonly bool _reloadOnChange;
    private readonly Lock _gate = new();

    private T? _cached;
    private FileStamp _cachedStamp;

    public JsonFileLoader(string configDirectory, string fileName, bool reloadOnChange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        _filePath = Path.Combine(configDirectory, fileName);
        _fileName = fileName;
        _reloadOnChange = reloadOnChange;
    }

    public T Load()
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

            _cached = Read();
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

    private T Read()
    {
        var json = File.ReadAllText(_filePath);

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonConfiguration.SerializerOptions)
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
