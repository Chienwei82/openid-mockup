using OidcMock.Core.Authorization;
using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.Tokens;

namespace OidcMock.Core.EndSession;

/// <summary>
/// Caso de uso del cierre de sesion. El orden importa: primero se resuelve quien cierra (el
/// <c>id_token_hint</c> manda sobre el <c>client_id</c>, porque es el que va firmado), despues se valida
/// la redireccion, y solo despues se cierra la sesion: una peticion invalida no debe tirar la sesion de
/// un navegador que solo esta probando una URL.
/// </summary>
public sealed class EndSessionService(
    IClientStore clientStore,
    IAuthSessionStore sessions,
    IIdTokenReader idTokenReader) : IEndSessionService
{
    private const string UnknownClient =
        "El id_token_hint no corresponde a ningun cliente registrado en el mock.";
    private const string UnknownPostLogoutRedirectUri =
        "El post_logout_redirect_uri no esta registrado para el cliente.";
    private const string HintTakesPrecedence =
        "El id_token_hint y el client_id no pueden corresponder a clientes distintos.";

    public Result<EndSessionResult> EndSession(EndSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = ResolveClient(request);
        if (client.Failed)
        {
            return Failure(client.Error!);
        }

        var redirect = ResolveRedirect(client.Value, request);
        if (redirect.Failed)
        {
            return Failure(redirect.Error!);
        }

        CloseSession(request.SessionId);

        return Result<EndSessionResult>.Ok(new EndSessionResult(
            request.Issuer,
            redirect.Value,
            request.State,
            client.Value?.FrontchannelLogoutUri,
            request.SessionId));
    }

    private static Result<EndSessionResult> Failure(ProtocolError error) =>
        Result<EndSessionResult>.Fail(error);

    /// <summary>
    /// El hint firmado es la fuente de verdad del cliente: si viene, su <c>aud</c> es el cliente contra
    /// el que se valida el redirect. Solo si no viene se usa el parametro <c>client_id</c>. Sin ninguno
    /// de los dos no hay cliente, y entonces solo se puede cerrar la sesion y mostrar la pagina.
    /// </summary>
    private Result<Client?> ResolveClient(EndSessionRequest request)
    {
        if (!string.IsNullOrEmpty(request.IdTokenHint))
        {
            return ClientFromHint(request);
        }

        return string.IsNullOrEmpty(request.ClientId)
            ? Result<Client?>.Ok(null)
            : FindClient(request.ClientId, UnknownClient);
    }

    private Result<Client?> ClientFromHint(EndSessionRequest request)
    {
        var hint = idTokenReader.Read(request.IdTokenHint!, request.Issuer);
        if (hint.Failed)
        {
            return Result<Client?>.Fail(hint.Error!);
        }

        var client = FindClient(hint.Value!.ClientId, UnknownClient);

        return client.Succeeded && !SameClient(client.Value!, request.ClientId)
            ? Result<Client?>.Fail(ProtocolErrors.InvalidRequest(HintTakesPrecedence))
            : client;
    }

    /// <summary>
    /// Sin <c>post_logout_redirect_uri</c> no hay nada que validar y tampoco nada que redirigir: el
    /// endpoint mostrara la pagina de cierre. Con el, tiene que estar registrado para ese cliente, que
    /// es la unica proteccion contra un open redirect.
    /// </summary>
    private Result<string?> ResolveRedirect(Client? client, EndSessionRequest request)
    {
        if (string.IsNullOrEmpty(request.PostLogoutRedirectUri))
        {
            return Result<string?>.Ok(null);
        }

        return IsRegistered(client, request.PostLogoutRedirectUri)
            ? Result<string?>.Ok(request.PostLogoutRedirectUri)
            : Result<string?>.Fail(ProtocolErrors.InvalidRequest(UnknownPostLogoutRedirectUri));
    }

    /// <summary>
    /// Con cliente identificado, la lista es la suya. Sin <c>client_id</c> ni <c>id_token_hint</c> no hay
    /// cliente contra el que validar, y se busca en todos: el redirect solo se acepta si esta
    /// registrado en algun cliente, que sigue impidiendo el open redirect.
    /// </summary>
    private bool IsRegistered(Client? client, string postLogoutRedirectUri) =>
        client is not null
            ? client.AllowsPostLogoutRedirectUri(postLogoutRedirectUri)
            : clientStore.List().Any(registered => registered.AllowsPostLogoutRedirectUri(postLogoutRedirectUri));

    private void CloseSession(string? sessionId)
    {
        if (!string.IsNullOrEmpty(sessionId))
        {
            sessions.Close(sessionId);
        }
    }

    private static bool SameClient(Client client, string? clientId) =>
        string.IsNullOrEmpty(clientId) || string.Equals(client.ClientId, clientId, StringComparison.Ordinal);

    private Result<Client?> FindClient(string clientId, string description)
    {
        var client = clientStore.Find(clientId);

        return client is null
            ? Result<Client?>.Fail(ProtocolErrors.InvalidRequest(description))
            : Result<Client?>.Ok(client);
    }
}