using OidcMock.Core.Clients;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Peticion de autorizacion ya validada contra la configuracion del mock, con los scopes filtrados
/// a los que el cliente tiene derecho.
/// </summary>
public sealed record ValidatedAuthorizationRequest(
    Client Client,
    string RedirectUri,
    IReadOnlyList<string> Scopes,
    string ResponseType,
    string ResponseMode,
    string? Nonce,
    string? State,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    string? Prompt,
    /// <summary>
    /// Referencia de la peticion empujada que origino esta, si la hubo. Viaja hasta las pantallas del
    /// mock y de ahi al canje del codigo, para que el <c>request_uri</c> caduque al approving.
    /// </summary>
    string? RequestUri = null)
{
    public bool IsImplicit => !ResponseType.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Contains(ResponseTypeNames.Code, StringComparer.Ordinal);
}