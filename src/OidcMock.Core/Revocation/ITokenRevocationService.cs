using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.Core.Tokens;

namespace OidcMock.Core.Revocation;

/// <summary>
/// Revoca un access token o un refresh token (RFC 7009). Solo el cliente propietario puede revocar.
/// </summary>
public interface ITokenRevocationService
{
    /// <summary>
    /// RFC 7009 seccion 2.2: un token desconocido se responde con exito igual que uno revocado, para no
    /// revelar si existio. Por eso devuelve Result y no un booleano de "se revoco algo".
    /// </summary>
    Result<bool> Revoke(
        string token,
        string issuer,
        IEnumerable<string> audiences,
        string requestingClientId);
}