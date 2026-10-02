using OidcMock.Core.Codes;
using OidcMock.Core.Errors;

namespace OidcMock.Core.Grants;

/// <summary>
/// Almacena los refresh tokens emitidos, con expiracion y de un solo uso: cada canje rota el token.
/// Un token ya canjeado se recuerda como rotado, para detectar su reutilizacion y revocar la
/// familia completa en lugar de fallar en silencio.
/// </summary>
public interface IRefreshTokenStore
{
    RefreshToken Issue(RefreshTokenRequest request);

    /// <summary>
    /// Canjea el token y lo consume. Devuelve invalid_grant si no existe, ya se canjeo o caduco.
    /// Si el token ya estaba rotado, revoca ademas toda su familia antes de fallar.
    /// </summary>
    Result<RefreshToken> Redeem(string token);

    /// <summary>
    /// Datos de un token que el mock emitio, este mismo o ya canjeado. Es lo que permite revocar un
    /// token que ya se canjeo: acordarse de el es lo unico que permite saber a que familia cortarle la
    /// sesion.
    /// </summary>
    RefreshToken? FindIssued(string token);

    void Revoke(string token);

    /// <summary>Revoca la familia a la que pertenece el token indicado.</summary>
    void RevokeFamily(string familyId);

    /// <summary>Descarta los refresh tokens caducados, para que no crezcan sin limite.</summary>
    void Expire();

    IReadOnlyList<RefreshToken> List();
}

/// <summary>
/// Datos con los que se emite un refresh token. Si <paramref name="FamilyId"/> viene informado, el
/// token hereda esa familia (rotacion); si no, abre una familia nueva.
/// </summary>
public sealed record RefreshTokenRequest(
    string ClientId,
    string Subject,
    IReadOnlyList<string> Scopes,
    TimeSpan Lifetime,
    string? FamilyId = null);