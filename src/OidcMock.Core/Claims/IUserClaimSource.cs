using System.Text.Json;
using OidcMock.Core.Users;

namespace OidcMock.Core.Claims;

/// <summary>
/// Resuelve el valor de un claim de protocolo a partir del usuario. Implementar un resolver nuevo
/// (por ejemplo, un claim derivado de varios datos del usuario) no obliga a tocar el proyector.
/// </summary>
public interface IUserClaimSource
{
    bool TryResolve(string claimName, User user, out JsonElement value);
}