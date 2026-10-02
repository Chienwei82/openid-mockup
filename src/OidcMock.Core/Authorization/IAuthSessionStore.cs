namespace OidcMock.Core.Authorization;

/// <summary>
/// Sesion de login del navegador, con la que el authorize recuerda quien esta autenticado. La
/// lleva una cookie propia del mock; el identificador es opaco y solo vale dentro del proceso.
/// </summary>
public sealed record AuthSession(
    string SessionId,
    string UserName,
    string Subject,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt)
{
    public bool IsExpiredAt(DateTimeOffset instant) => instant >= ExpiresAt;
}

/// <summary>
/// Estado de sesion en memoria del mock. Es la pieza que hace observables prompt=none, prompt=login
/// y el silencio SSO: sin ella, cada peticion al authorize seria una pantalla de login.
/// </summary>
public interface IAuthSessionStore
{
    /// <summary>Abre una sesion para un usuario ya autenticado y devuelve su identificador opaco.</summary>
    AuthSession Start(string userName, string subject);

    /// <summary>Sesion vigente con ese identificador, o null si no existe o ya caduco.</summary>
    AuthSession? Find(string sessionId);

    /// <summary>Cierra la sesion, para que el proximo authorize vuelva a pedir credenciales.</summary>
    void Close(string sessionId);

    /// <summary>Descarta las sesiones caducadas, para que no crezcan sin limite.</summary>
    void Expire();

    IReadOnlyList<AuthSession> List();
}
