using OidcMock.Core.Clients;
using OidcMock.Core.Codes;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Valida la peticion de autorizacion contra la configuracion del mock. Las comprobaciones son
/// reglas IAuthorizeRequestValidator independientes que se aplican en orden y gana la primera que
/// falle, asi que anadir una comprobacion es registrar otra regla, sin tocar las existentes.
/// </summary>
public sealed class AuthorizationRequestValidator(
    IClientStore clientStore,
    IEnumerable<IAuthorizeRequestValidator> validators) : IAuthorizationRequestValidator
{
    private readonly IAuthorizeRequestValidator[] _validators = validators?.ToArray()
        ?? throw new ArgumentNullException(nameof(validators));

    private readonly IClientStore _clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));

    public AuthorizationValidationResult Validate(AuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = new AuthorizeValidationContext(request, ResolveClient(request.ClientId));

        foreach (var validator in _validators)
        {
            if (validator.Validate(context) is not { } error)
            {
                continue;
            }

            return AuthorizationValidationResult.Failed(
                error,
                validator.ErrorIsRedirectable ? request.RedirectUri : null,
                request.State);
        }

        return AuthorizationValidationResult.Ok(Build(context));
    }

    private Client? ResolveClient(string? clientId) =>
        string.IsNullOrEmpty(clientId) ? null : _clientStore.Find(clientId);

    /// <summary>
    /// Sin code_challenge_method, PKCE usa plain, que es el valor por defecto de RFC 7636 4.3. El
    /// valor resuelto queda en la peticion validada para poder verificar el challenge al canjear.
    /// </summary>
    private static ValidatedAuthorizationRequest Build(AuthorizeValidationContext context)
    {
        var request = context.Request;

        return new ValidatedAuthorizationRequest(
            context.Client!,
            request.RedirectUri!,
            request.EffectiveScopes,
            request.ResponseType!,
            request.ResponseMode,
            request.Nonce,
            request.State,
            request.CodeChallenge,
            DefaultedCodeChallengeMethod(request),
            string.IsNullOrWhiteSpace(request.Prompt) ? null : request.Prompt,
            request.RequestUri);
    }

    private static string? DefaultedCodeChallengeMethod(AuthorizationRequest request) =>
        !string.IsNullOrEmpty(request.CodeChallenge) && string.IsNullOrEmpty(request.CodeChallengeMethod)
            ? PkceCodeChallengeMethods.Plain
            : request.CodeChallengeMethod;
}
