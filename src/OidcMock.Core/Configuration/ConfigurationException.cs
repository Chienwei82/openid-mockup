using System.Globalization;

namespace OidcMock.Core.Configuration;

/// <summary>
/// Error de configuracion legible para el operador: indica el archivo problematico y el motivo.
/// </summary>
public sealed class ConfigurationException : Exception
{
    public ConfigurationException(string fileName, string reason, Exception? innerException = null)
        : base(string.Create(CultureInfo.InvariantCulture, $"Error de configuracion en '{fileName}': {reason}"), innerException)
    {
        FileName = fileName;
        Reason = reason;
    }

    public string FileName { get; }

    public string Reason { get; }
}
