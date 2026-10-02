namespace OidcMock.Host.Logging;

/// <summary>
/// Eventos del mock con <c>LoggerMessage</c> source generator: el mensaje y el EventId se compilan
/// una vez y no se formatea cadena si el nivel esta desactivado.
///
/// Nunca se registra el token, el codigo, el refresh token ni el secreto del cliente: solo su
/// tipo, su cliente y su caducidad. Un log de desarrollo acaba en consola, en el pipe de CI o en un
/// archivo compartido, y un token completo ahi es una credencial viva.
/// </summary>
public static partial class OidcMockLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Configuracion cargada desde '{ConfigDirectory}' (recarga en caliente: {ReloadOnChange})")]
    public static partial void ConfigurationLoaded(ILogger logger, string configDirectory, bool reloadOnChange);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "El mock escucha con Issuer '{Issuer}' y PathBase '{PathBase}'")]
    public static partial void ServingStarted(ILogger logger, string issuer, string pathBase);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "No se pudo escribir la clave de firma en '{KeyPath}' ({Reason}). El mock sigue " +
                  "firmando, pero el kid cambiara en cada reinicio y los tokens emitidos antes dejaran de validar.")]
    public static partial void SigningKeyNotPersisted(ILogger logger, string keyPath, string reason);

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Login correcto del usuario '{Subject}' para el cliente '{ClientId}'")]
    public static partial void LoginSucceeded(ILogger logger, string subject, string clientId);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Login fallido del usuario '{UserName}' para el cliente '{ClientId}'")]
    public static partial void LoginFailed(ILogger logger, string userName, string clientId);

    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Information,
        Message = "Codigo de autorizacion emitido al cliente '{ClientId}' para el sujeto '{Subject}'")]
    public static partial void AuthorizationCodeIssued(ILogger logger, string clientId, string subject);

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "Tokens emitidos por '{GrantType}' al cliente '{ClientId}' (caduca en {ExpiresInSeconds}s, " +
                  "refresh emitido: {RefreshTokenIssued})")]
    public static partial void TokensIssued(
        ILogger logger,
        string grantType,
        string clientId,
        int expiresInSeconds,
        bool refreshTokenIssued);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "El token endpoint rechazo la peticion con '{Error}' para el cliente '{ClientId}' " +
                  "usando '{Method}'")]
    public static partial void TokenRequestRejected(ILogger logger, string error, string clientId, string method);

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Warning,
        Message = "El cliente '{ClientId}' fallo al autenticarse con '{Method}'")]
    public static partial void ClientAuthenticationFailed(ILogger logger, string clientId, string method);

    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Information,
        Message = "Sesion cerrada en end_session para '{Subject}'")]
    public static partial void SessionEnded(ILogger logger, string subject);

    [LoggerMessage(
        EventId = 9000,
        Level = LogLevel.Error,
        Message = "Error no previsto en '{Method} {Path}'; el traceId de la respuesta es '{TraceId}'")]
    public static partial void UnexpectedError(
        ILogger logger,
        Exception exception,
        string method,
        string path,
        string traceId);
}