using OidcMock.Core.Errors;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Concede (o deniega) una autorizacion: valida las credenciales del usuario contra el IUserStore y,
/// si son correctas, emite el codigo de autorizacion.
/// </summary>
public interface IAuthorizationService
{
    Result<AuthorizationGranted> SignIn(SignInRequest request);

    Result<AuthorizationGranted> Deny(ValidatedAuthorizationRequest request);
}