using System.Text;
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

    private CacheEntry? _cached;

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
            if (_cached is { } entry && !_reloadOnChange)
            {
                return entry.Domain;
            }

            var currentContent = ReadContent();

            if (_cached is { } cached && currentContent.AsSpan().SequenceEqual(cached.Content))
            {
                return cached.Domain;
            }

            _cached = new CacheEntry(_toDomain(Deserialize(currentContent)), currentContent);
            return _cached.Domain;
        }
    }

    private byte[] ReadContent()
    {
        if (!File.Exists(_filePath))
        {
            throw new ConfigurationException(_fileName, "el archivo no existe en el directorio de configuracion.");
        }

        return File.ReadAllBytes(_filePath);
    }

    private TFile Deserialize(byte[] content)
    {
        var json = Encoding.UTF8.GetString(content);

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

    private sealed record CacheEntry(TDomain Domain, byte[] Content);
}
