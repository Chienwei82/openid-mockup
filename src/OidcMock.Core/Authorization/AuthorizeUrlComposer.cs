using OidcMock.Core.Clients;
using OidcMock.Core.Codes;
using OidcMock.Core.Discovery;

namespace OidcMock.Core.Authorization;

/// <summary>
/// La URL de authorize lista para pegar en el navegador mas el code_verifier que la acompana, cuando
/// el cliente exige PKCE. El verifier es el que se necesita despues para canjear el code.
/// </summary>
public sealed record DemoAuthorizeUrl(string Url, string? CodeVerifier);

/// <summary>
/// Compone la URL de <c>connect/authorize/callback</c> con los valores del cliente precargados, para
/// que la pantalla raiz del mock ofrezca una entrada de un clic. Es dominio puro: no conoce el host.
/// </summary>
public static class AuthorizeUrlComposer
{
    private const string DefaultState = "state-de-prueba";
    private const string DefaultNonce = "nonce-de-prueba";

    /// <summary>
    /// Scopes que se precargan de los permitidos: los estandar de un login, en un orden legible.
    /// </summary>
    private static readonly string[] PreferredScopes = ["openid", "profile", "email", "offline_access"];

    public static DemoAuthorizeUrl Compose(string issuer, Client client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentNullException.ThrowIfNull(client);

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = client.ClientId,
            ["redirect_uri"] = client.RedirectUris.Count > 0 ? client.RedirectUris[0] : string.Empty,
            ["response_type"] = ResponseTypeNames.Code,
            ["scope"] = string.Join(' ', PreferredScopes.Where(client.AllowsScope)),
            ["state"] = DefaultState,
            ["nonce"] = DefaultNonce
        };

        return new DemoAuthorizeUrl(BuildUrl(issuer, parameters, client.RequirePkce, out var verifier), verifier);
    }

    private static string BuildUrl(
        string issuer,
        Dictionary<string, string> parameters,
        bool requirePkce,
        out string? verifier)
    {
        verifier = requirePkce ? AddPkce(parameters) : null;
        var baseUrl = EndpointUri.Combine(issuer, EndpointPaths.AuthorizeCallback);

        return $"{baseUrl}?{ToQueryString(parameters)}";
    }

    private static string AddPkce(Dictionary<string, string> parameters)
    {
        var verifier = CodeVerifier.New();
        parameters["code_challenge"] = CodeChallenges.Create(PkceCodeChallengeMethods.Sha256, verifier);
        parameters["code_challenge_method"] = PkceCodeChallengeMethods.Sha256;

        return verifier;
    }

    private static string ToQueryString(IReadOnlyDictionary<string, string> parameters) =>
        string.Join(
            '&',
            parameters.Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
}
