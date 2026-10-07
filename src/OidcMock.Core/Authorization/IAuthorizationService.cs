using OidcMock.Core.Errors;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Concede una autorizacion a nombre de la identidad que la pantalla del mock dejo elegir, y emite
/// el codigo de autorizacion.
/// </summary>
public interface IAuthorizationService
{
    Result<AuthorizationGranted> Approve(AuthorizationApproval approval);
}

/// <summary>Otorgamiento de una autorizacion a nombre de una identidad.</summary>
public sealed record AuthorizationApproval(string UserName, ValidatedAuthorizationRequest Authorization, string? SessionId = null);