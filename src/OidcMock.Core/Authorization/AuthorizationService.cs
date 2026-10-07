using OidcMock.Core.Codes;
using OidcMock.Core.Errors;
using OidcMock.Core.Users;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Caso de uso de la pantalla de login del mock. Valida las credenciales con el UserAuthenticator
/// y emite el codigo de autorizacion. Para un mock offline la contrasena esta en claro en users.json.
/// </summary>
public sealed class AuthorizationService(ICodeStore codeStore, UserAuthenticator authenticator) : IAuthorizationService
{
    private const string InvalidCredentials = "El usuario o la contrasena no son correctos.";

    public Result<AuthorizationGranted> SignIn(SignInRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = authenticator.Authenticate(request.UserName, request.Password);

        return user is null
            ? Result<AuthorizationGranted>.Fail(AuthorizationErrors.AccessDenied(InvalidCredentials))
            : Approve(new AuthorizationApproval(user.UserName, request.Authorization));
    }

    public Result<AuthorizationGranted> Approve(AuthorizationApproval approval)
    {
        ArgumentNullException.ThrowIfNull(approval);

        var authorization = approval.Authorization;
        var code = codeStore.Issue(new AuthorizationCodeRequest(
            authorization.Client.ClientId,
            approval.UserName,
            authorization.Scopes,
            authorization.Nonce,
            authorization.State,
            authorization.CodeChallenge,
            authorization.CodeChallengeMethod,
            authorization.Client.TokenLifetimes.AuthorizationCode,
            authorization.RedirectUri,
            approval.SessionId));

        return Result<AuthorizationGranted>.Ok(new AuthorizationGranted(code));
    }
}