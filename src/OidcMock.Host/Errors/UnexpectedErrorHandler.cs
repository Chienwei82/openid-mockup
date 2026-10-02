using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OidcMock.Host.Logging;

namespace OidcMock.Host.Errors;

/// <summary>
/// Traduce una excepcion no prevista a ProblemDetails. Los errores de protocolo no pasan por aqui:
/// los endpoints los forman ellos mismos con el cuerpo OAuth de RFC 6749 5.2, y un cliente que
/// espera <c>invalid_grant</c> no debe recibir un 500 con otro formato.
/// </summary>
public sealed class UnexpectedErrorHandler(ILogger<UnexpectedErrorHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // El detalle se queda en el log: la respuesta solo lleva traceId, que es lo que permite
        // correlacionar el fallo del servidor con el del cliente.
        OidcMockLog.UnexpectedError(
            logger,
            exception,
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Se ha producido un error inesperado en el servidor de autenticacion.",
            Detail = "Revisa el log del servidor con el traceId de esta respuesta.",
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problem.Status!.Value;
        // WriteAsJsonAsync con su propio contentType: si se deja application/json, un cliente no
        // distingue esta respuesta de un cuerpo OAuth, que es justo lo que hay que evitar.
        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}