namespace OidcMock.Core.Grants;

/// <summary>
/// Refresh token emitido por el mock: opaco, associated al cliente y al subject que lo pidio, y
/// con vigencia propia. Vive en memoria, como los codigos de autorizacion.
/// </summary>
public sealed record RefreshToken(
    string Token,
    string ClientId,
    string Subject,
    IReadOnlyList<string> Scopes,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt)
{
    public bool IsExpiredAt(DateTimeOffset instant) => instant >= ExpiresAt;
}