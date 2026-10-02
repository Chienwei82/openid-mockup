namespace OidcMock.Core.Revocation;

/// <summary>
/// Registro de los tokens revocados, compartido por todos los que necesitan saber si un token sigue
/// sirviendo: /connect/userinfo, /connect/introspect y los grants del token endpoint.
/// <para>
/// Existe porque el access token del mock es un JWT sin estado: no hay nada que borrar en el token,
/// asi que revocar significa apuntar su <c>jti</c> aqui. Los refresh tokens se revocan ademas en su
/// propio store (que los consume), pero la familia se recuerda aqui para que la revocacion en cascada
/// tenga una unica fuente de verdad.
/// </para>
/// <para>
/// Las revocaciones caducan con el token al que corresponden: una vez que el token habria caducado
/// solo, el registro es basura y se descarta, para que el store no crezca sin limite.
/// </para>
/// </summary>
public interface ITokenRevocationStore
{
    /// <summary>
    /// Registra la revocacion de un access token por su <c>jti</c>. <paramref name="expiresAt"/> es el
    /// <c>exp</c> del token: a partir de ahi la revocacion ya no se distingue de un token caducado.
    /// </summary>
    void RevokeAccessToken(string tokenId, DateTimeOffset expiresAt);

    /// <summary>
    /// Registra la revocacion de una familia de refresh tokens entera, con la vigencia del token que
    /// la origino.
    /// </summary>
    void RevokeRefreshTokenFamily(string familyId, DateTimeOffset expiresAt);

    bool IsAccessTokenRevoked(string tokenId);

    bool IsRefreshTokenFamilyRevoked(string familyId);

    /// <summary>Descarta las revocaciones cuyo token ya habria caducado.</summary>
    void Expire();
}