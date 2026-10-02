namespace OidcMock.Core.Grants;

/// <summary>
/// Refresh token emitido por el mock: opaco, associated al cliente y al subject que lo pidio, y
/// con vigencia propia. Los tokens de una misma familia comparten <see cref="FamilyId"/>, de modo
/// que la rotacion conserve la familia y una reutilizacion pueda revocarla entera.
/// </summary>
public sealed record RefreshToken(
    string Token,
    string ClientId,
    string Subject,
    IReadOnlyList<string> Scopes,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string FamilyId)
{
    public bool IsExpiredAt(DateTimeOffset instant) => instant >= ExpiresAt;
}