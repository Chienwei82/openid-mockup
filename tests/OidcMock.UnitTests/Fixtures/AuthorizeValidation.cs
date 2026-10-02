using OidcMock.Core.Authorization;
using OidcMock.Core.Clients;
using OidcMock.Core.Grants;
using OidcMock.Core.Scopes;

namespace OidcMock.UnitTests.Fixtures;

/// <summary>
/// Construye peticiones de autorizacion para probar validadores sueltos, sin levantar el host.
/// Un parametro a null significa "no enviar", para que cada test se centre en su regla. El cliente
/// se resuelve del store a partir del client_id, igual que hace la cadena, para que un test de
/// "cliente desconocido" no se contradiga a si mismo.
/// </summary>
public static class AuthorizeValidation
{
    private static readonly IClientStore Clients = ClientStoreFixture.Create();

    public const string RedirectUri = "https://localhost:5173/callback";
    public const string CodeChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    public static AuthorizeValidationContext Context(
        Client? client = null,
        string? clientId = ClientStoreFixture.SpaClientId,
        string? redirectUri = RedirectUri,
        string? responseType = ResponseTypeNames.Code,
        IReadOnlyList<string>? scopes = null,
        string? nonce = null,
        string? state = null,
        string? codeChallenge = CodeChallenge,
        string? codeChallengeMethod = null,
        string? prompt = null,
        string? grantType = GrantTypes.AuthorizationCode,
        string? responseMode = ResponseModes.Query) =>
        new(
            new AuthorizationRequest(
                clientId,
                redirectUri,
                responseType,
                scopes ?? [ScopeNames.OpenId, ScopeNames.Email],
                nonce,
                state,
                codeChallenge,
                codeChallengeMethod,
                prompt,
                grantType,
                responseMode ?? ResponseModes.Query),
            client ?? Clients.Find(clientId!));
}
