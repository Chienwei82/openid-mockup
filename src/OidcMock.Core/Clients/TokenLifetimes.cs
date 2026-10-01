namespace OidcMock.Core.Clients;

/// <summary>
/// Vigencia de los distintos artefactos que emite el servidor para un cliente.
/// </summary>
public sealed record TokenLifetimes(
    TimeSpan AccessToken,
    TimeSpan IdentityToken,
    TimeSpan RefreshToken,
    TimeSpan AuthorizationCode);
