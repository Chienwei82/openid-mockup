using OidcMock.Core.Clients;
using OidcMock.Core.Codes;
using OidcMock.Core.Errors;
using OidcMock.Core.Scopes;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Valida la peticion de autorizacion contra la configuracion del mock. Cada comprobacion es una
/// AuthorizationRule independiente que devuelve null cuando la cumple, y se aplican en orden con
/// el primer fallo. Anadir una regla es agregar una entrada a la lista, sin tocar las existentes.
/// </summary>
public sealed class AuthorizationRequestValidator : IAuthorizationRequestValidator
{
    private static readonly string[] SupportedPrompts = ["none", "login", "consent", "select_account"];

    private static readonly string[] SupportedResponseModes =
        [ResponseModes.Query, ResponseModes.Fragment, ResponseModes.FormPost];

    private readonly IClientStore _clientStore;
    private readonly IScopeStore _scopeStore;
    private readonly AuthorizationRule[] _rules;

    public AuthorizationRequestValidator(IClientStore clientStore, IScopeStore scopeStore)
    {
        ArgumentNullException.ThrowIfNull(clientStore);
        ArgumentNullException.ThrowIfNull(scopeStore);

        _clientStore = clientStore;
        _scopeStore = scopeStore;
        _rules =
        [
            ValidateGrantType,
            ValidateResponseType,
            ValidateScopes,
            ValidateCodeChallenge,
            ValidatePrompt,
            ValidateResponseMode
        ];
    }

    public AuthorizationValidationResult Validate(AuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = ResolveClient(request.ClientId);
        if (client is null)
        {
            return AuthorizationValidationResult.Failed(
                AuthorizationErrors.InvalidClient($"El cliente '{request.ClientId}' no esta registrado."));
        }

        if (!client.AllowsRedirectUri(request.RedirectUri ?? string.Empty))
        {
            return AuthorizationValidationResult.Failed(
                ProtocolErrors.InvalidRequest($"El redirect_uri '{request.RedirectUri}' no esta registrado para el cliente."));
        }

        // A partir de aqui el redirect_uri esta registrado, asi que los errores si pueden viajar por el.
        var firstError = _rules
            .Select(rule => rule(client, request))
            .FirstOrDefault(error => error is not null);

        return firstError is null
            ? AuthorizationValidationResult.Ok(Build(client, request))
            : AuthorizationValidationResult.Failed(firstError, request.RedirectUri, request.State);
    }

    private Client? ResolveClient(string? clientId) =>
        string.IsNullOrEmpty(clientId) ? null : _clientStore.Find(clientId);

    private static ProtocolError? ValidateGrantType(Client client, AuthorizationRequest request) =>
        client.AllowsGrantType(request.GrantType ?? string.Empty)
            ? null
            : ProtocolErrors.UnauthorizedClient(
                $"El cliente '{client.ClientId}' no puede usar el grant_type '{request.GrantType}'.");

    private static ProtocolError? ValidateResponseType(Client client, AuthorizationRequest request) =>
        ResponseTypeNames.SupportedCombinations.Contains(request.ResponseType ?? string.Empty, StringComparer.Ordinal)
            ? null
            : ProtocolErrors.UnsupportedResponseType($"El response_type '{request.ResponseType}' no esta soportado.");

    private ProtocolError? ValidateScopes(Client client, AuthorizationRequest request) =>
        RequestedScopes(request).All(scope => client.AllowsScope(scope) && IsKnownScope(scope))
            ? null
            : ProtocolErrors.InvalidScope(
                "Uno de los scopes solicitados no esta permitido para el cliente o no existe en el mock.");

    private static ProtocolError? ValidateCodeChallenge(Client client, AuthorizationRequest request)
    {
        var hasChallenge = !string.IsNullOrEmpty(request.CodeChallenge);
        var hasMethod = !string.IsNullOrEmpty(request.CodeChallengeMethod);

        if (hasMethod && !CodeChallenges.IsSupported(request.CodeChallengeMethod!))
        {
            return ProtocolErrors.InvalidRequest(
                $"El code_challenge_method '{request.CodeChallengeMethod}' no esta soportado.");
        }

        return client.RequirePkce && !hasChallenge
            ? ProtocolErrors.InvalidRequest($"El cliente '{client.ClientId}' requiere PKCE.")
            : null;
    }

    private static ProtocolError? ValidatePrompt(Client client, AuthorizationRequest request) =>
        string.IsNullOrWhiteSpace(request.Prompt) || SupportedPrompts.Contains(request.Prompt, StringComparer.Ordinal)
            ? null
            : ProtocolErrors.InvalidRequest($"El prompt '{request.Prompt}' no esta soportado.");

    private static ProtocolError? ValidateResponseMode(Client client, AuthorizationRequest request) =>
        SupportedResponseModes.Contains(request.ResponseMode, StringComparer.Ordinal)
            ? null
            : ProtocolErrors.InvalidRequest($"El response_mode '{request.ResponseMode}' no esta soportado.");

    private bool IsKnownScope(string scope) => _scopeStore.Find(scope) is not null;

    private static IReadOnlyList<string> RequestedScopes(AuthorizationRequest request) =>
        request.Scopes.Count > 0 ? request.Scopes : [ScopeNames.OpenId];

    /// <summary>
    /// Sin code_challenge_method, PKCE usa plain, que es el valor por defecto de RFC 7636 4.3. El
    /// valor resuelto queda en la peticion validada para poder verificar el challenge al canjear.
    /// </summary>
    private static string? DefaultedCodeChallengeMethod(AuthorizationRequest request) =>
        !string.IsNullOrEmpty(request.CodeChallenge) && string.IsNullOrEmpty(request.CodeChallengeMethod)
            ? PkceCodeChallengeMethods.Plain
            : request.CodeChallengeMethod;

    private static ValidatedAuthorizationRequest Build(Client client, AuthorizationRequest request) => new(
        client,
        request.RedirectUri!,
        RequestedScopes(request),
        request.ResponseType!,
        request.ResponseMode,
        request.Nonce,
        request.State,
        request.CodeChallenge,
        DefaultedCodeChallengeMethod(request),
        string.IsNullOrWhiteSpace(request.Prompt) ? null : request.Prompt);
}