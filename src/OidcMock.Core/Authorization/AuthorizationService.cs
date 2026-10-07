using OidcMock.Core.Codes;
using OidcMock.Core.Errors;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Emite el codigo de autorizacion para la identidad aprobada. Un mockup no autentica: la pantalla
/// de identidad decide quien es el usuario y este caso de uso solo registra la decision.
/// </summary>
public sealed class AuthorizationService(ICodeStore codeStore) : IAuthorizationService
{
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