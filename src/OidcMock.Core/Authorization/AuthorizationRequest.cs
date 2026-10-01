namespace OidcMock.Core.Authorization;

/// <summary>
/// Peticion de autorizacion tal como llega del cliente, ya deserializada pero sin validar.
/// </summary>
public sealed record AuthorizationRequest(
    string? ClientId,
    string? RedirectUri,
    string? ResponseType,
    IReadOnlyList<string> Scopes,
    string? Nonce,
    string? State,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    string? Prompt,
    string? GrantType,
    string ResponseMode);