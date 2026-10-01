namespace OidcMock.Core.Codes;

/// <summary>
/// Datos con los que el endpoint de autorizacion crea un codigo. Separate del codigo emitido para
/// que el store no exponga como se construye uno nuevo.
/// </summary>
public sealed record AuthorizationCodeRequest(
    string ClientId,
    string UserName,
    IReadOnlyList<string> Scopes,
    string? Nonce,
    string? State,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    TimeSpan Lifetime);