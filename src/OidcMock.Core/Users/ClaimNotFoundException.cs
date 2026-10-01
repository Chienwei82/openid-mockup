using System.Globalization;

namespace OidcMock.Core.Users;

/// <summary>
/// Se lanza cuando se pide un claim que el usuario no tiene configurado.
/// </summary>
public sealed class ClaimNotFoundException : Exception
{
    public ClaimNotFoundException(string subject, string claimName)
        : base(string.Create(CultureInfo.InvariantCulture, $"El usuario '{subject}' no tiene configurado el claim '{claimName}'."))
    {
    }
}
