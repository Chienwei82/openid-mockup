using Microsoft.AspNetCore.Http;
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
