using OidcMock.Core.Errors;

namespace OidcMock.Core.Codes;

/// <summary>
/// Almacena los codigos de autorizacion emitidos, con expiracion y de un solo uso.
/// </summary>
public interface ICodeStore
{
    /// <summary>Emite un codigo nuevo para una peticion de autorizacion ya autenticada.</summary>
    AuthorizationCode Issue(AuthorizationCodeRequest request);

    /// <summary>
    /// Canjea el codigo y lo consume. Devuelve invalid_grant si no existe, ya se canjeo o caduco.
    /// </summary>
    Result<AuthorizationCode> Redeem(string code);

    /// <summary>Descarta los codigos caducados, para que no crezcan sin limite.</summary>
    void Expire();

    IReadOnlyList<AuthorizationCode> List();
}