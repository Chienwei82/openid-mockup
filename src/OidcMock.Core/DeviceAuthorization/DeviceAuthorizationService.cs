using OidcMock.Core.Authorization;
using OidcMock.Core.Codes;
using OidcMock.Core.Errors;
using OidcMock.Core.PendingRequests;

namespace OidcMock.Core.DeviceAuthorization;

/// <summary>
/// Emite el device code y el user code. El intervalo de sondeo es el que RFC 8628 sugiere para el
/// token endpoint: cinco segundos si el cliente no pide otro.
/// </summary>
public sealed class DeviceAuthorizationService(IPendingAuthorizationStore pendingRequests, TimeProvider timeProvider)
    : IDeviceAuthorizationService
{
    private const int DefaultIntervalInSeconds = 5;

    private static readonly TimeSpan DeviceCodeLifetime = TimeSpan.FromMinutes(10);

    public Result<DeviceAuthorizationResponse> Start(DeviceAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issuedAt = timeProvider.GetUtcNow();
        var deviceCode = OpaqueToken.New();
        var userCode = UserCodeGenerator.New();

        pendingRequests.Issue(new PendingAuthorizationRequest(
            deviceCode,
            request.Client.ClientId,
            request.Scopes,
            AuthorizationWithoutRedirect(request),
            string.Empty,
            issuedAt + DeviceCodeLifetime,
            TimeSpan.FromSeconds(DefaultIntervalInSeconds),
            deviceCode,
            userCode));

        return Result<DeviceAuthorizationResponse>.Ok(new DeviceAuthorizationResponse(
            deviceCode,
            userCode,
            VerificationUri.Constant,
            (int)DeviceCodeLifetime.TotalSeconds,
            DefaultIntervalInSeconds));
    }

    /// <summary>
    /// El flujo de device no tiene redirect_uri: se construye una peticion con el cliente real y sin
    /// redireccion, para poder reutilizar el proyector de claims y la fabricacion de tokens.
    /// </summary>
    private static ValidatedAuthorizationRequest AuthorizationWithoutRedirect(DeviceAuthorizationRequest request) =>
        new(
            request.Client,
            string.Empty,
            request.Scopes,
            ResponseTypeNames.Code,
            ResponseModes.Query,
            Nonce: null,
            State: null,
            CodeChallenge: null,
            CodeChallengeMethod: null,
            Prompt: null);
}

/// <summary>
/// User code corto y legible para que el usuario lo escriba en otro dispositivo.
/// </summary>
public static class UserCodeGenerator
{
    private const int UserCodeLength = 8;
    private const string Alphabet = "BCDFGHJKLMNPQRSTVWXZ";

    public static string New()
    {
        var random = System.Security.Cryptography.RandomNumberGenerator.GetBytes(UserCodeLength);

        return new string([.. random.Select(value => Alphabet[value % Alphabet.Length])]);
    }
}

/// <summary>
/// URI donde el usuario introduce el user code. El mock no necesita un PathBase propio: cuelga del
/// issuer que el cliente ya conoce.
/// </summary>
public static class VerificationUri
{
    public const string Constant = "urn:oidc-mock:device";
}