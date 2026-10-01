using OidcMock.Core.Codes;
using OidcMock.Core.Errors;
using OidcMock.Core.Users;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Caso de uso de la pantalla de login del mock. Valida las credenciales contra el IUserStore y
/// emite el codigo de autorizacion. Para un mock offline la contrasena esta en claro en users.json,
/// pero se compara en tiempo constante para no filtrar informacion por temporizacion.
/// </summary>
public sealed class AuthorizationService(ICodeStore codeStore, IUserStore userStore) : IAuthorizationService
{
    private const string InvalidCredentials = "El usuario o la contrasena no son correctos.";

    public Result<AuthorizationGranted> SignIn(SignInRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = userStore.FindByUserName(request.UserName);

        if (user is null || !CredentialsMatch(user, request.Password))
        {
            return Result<AuthorizationGranted>.Fail(AuthorizationErrors.AccessDenied(InvalidCredentials));
        }

        var authorization = request.Authorization;
        var code = codeStore.Issue(new AuthorizationCodeRequest(
            authorization.Client.ClientId,
            user.UserName,
            authorization.Scopes,
            authorization.Nonce,
            authorization.State,
            authorization.CodeChallenge,
            authorization.CodeChallengeMethod,
            authorization.Client.TokenLifetimes.AuthorizationCode));

        return Result<AuthorizationGranted>.Ok(new AuthorizationGranted(code));
    }

    public Result<AuthorizationGranted> Deny(ValidatedAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Result<AuthorizationGranted>.Fail(
            AuthorizationErrors.AccessDenied("El usuario denego la autorizacion en la pantalla de login."));
    }

    private static bool CredentialsMatch(User user, string password) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(user.Password),
            System.Text.Encoding.UTF8.GetBytes(password ?? string.Empty));
}