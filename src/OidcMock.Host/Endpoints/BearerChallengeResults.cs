using Microsoft.AspNetCore.Http;
using OidcMock.Core.Clients;
using OidcMock.Core.Errors;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Respuestas 401 de un recurso protegido por tokens: el cuerpo JSON de error de siempre y, sobre todo,
/// el encabezado WWW-Authenticate con el reto Bearer (RFC 6750 3), sin el cual el cliente no sabe que
/// credencial debe presentar.
/// </summary>
public static class BearerChallengeResults
{
    /// <summary>401 con el reto y el codigo de error: el token vino y no sirvio.</summary>
    public static IResult InvalidToken(HttpContext context, ProtocolError error)
    {
        context.Response.Headers.WWWAuthenticate = BearerChallenge.ForInvalidToken(error.Description);

        return ProtocolErrorResults.From(error);
    }

    /// <summary>
    /// 401 con el reto sin codigo de error: la peticion no traia credenciales, y RFC 6750 3.1 prohibe
    /// inventar un motivo que no se conoce.
    /// </summary>
    public static IResult WithoutCredentials(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = BearerChallenge.WithoutError();

        return Results.StatusCode(StatusCodes.Status401Unauthorized);
    }
}

/// <summary>
/// Lee las credenciales del cliente de una peticion: el encabezado Authorization gana al cuerpo
/// (RFC 6749 2.3.1), igual que en el token endpoint, para que un endpoint no tenga su propia idea de
/// como se presenta un cliente.
/// </summary>
public static class ClientCredentialsReader
{
    public static ClientCredentials Read(HttpRequest request, IReadOnlyDictionary<string, string> values) =>
        BasicAuthorizationHeader.TryRead(request, out var fromHeader)
            ? new ClientCredentials(
                ClientAuthenticationMethods.ClientSecretBasic,
                fromHeader.ClientId,
                fromHeader.Secret)
            : new ClientCredentials(
                ClientAuthenticationMethods.ClientSecretPost,
                values.GetValueOrDefault("client_id"),
                values.GetValueOrDefault("client_secret"));
}