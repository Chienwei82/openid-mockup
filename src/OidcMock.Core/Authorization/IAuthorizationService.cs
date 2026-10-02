using OidcMock.Core.Errors;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Concede (o deniega) una autorizacion: valida las credenciales del usuario contra el IUserStore y,
/// si son correctas, emite el codigo de autorizacion.
/// </summary>
public interface IAuthorizationService
{
    Result<AuthorizationGranted> SignIn(SignInRequest request);

    /// <summary>
    /// Emite el codigo para un usuario ya autenticado por una sesion previa, sin volver a pedir
    /// credenciales. Es el camino silencioso de prompt=none y el de la segunda peticion en adelante.
    /// </summary>
    Result<AuthorizationGranted> Approve(AuthorizationApproval approval);

    Result<AuthorizationGranted> Deny(ValidatedAuthorizationRequest request);
}

/// <summary>Otorgamiento de una autorizacion a nombre de un usuario ya autenticado.</summary>
public sealed record AuthorizationApproval(string UserName, ValidatedAuthorizationRequest Authorization);