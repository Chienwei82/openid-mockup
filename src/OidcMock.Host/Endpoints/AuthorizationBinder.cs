using OidcMock.Core.Authorization;
using OidcMock.Core.Errors;
using OidcMock.Core.PushedRequests;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Enlaza y valida una peticion de autorizacion. El GET y el POST del authorize hacen exactamente lo
/// mismo hasta decidir la pantalla, asi que compartir este paso evita que uno de los dos caminos se
/// quede atras.
/// </summary>
internal sealed class AuthorizationBinder(
    IAuthorizationRequestValidator validator,
    IPushedAuthorizationService pushedRequests)
{
    /// <summary>
    /// Resultado de enlazar: la peticion validada o el error con la respuesta que hay que devolver.
    ///
    /// El error se guarda como <see cref="AuthorizationValidationResult"/> y no como
    /// <see cref="ProtocolError"/> porque el validador ya decidio si puede redirigirse al
    /// <c>redirect_uri</c> del cliente: solo cuando el <c>client_id</c> y el <c>redirect_uri</c> son
    /// validos, para no convertir el authorize en un vector de open redirect.
    /// </summary>
    /// <remarks>
    /// El metodo se llama <c>Bind</c> y no <c>BindAsync</c> a proposito: Minimal API busca por
    /// convencion un <c>BindAsync</c> en los tipos que inyecta y lo toma por un binder de parametros,
    /// que es otra cosa.
    /// </remarks>
    public sealed record Binding(AuthorizationValidationResult Validation, AuthorizationRequest? Bound)
    {
        public ValidatedAuthorizationRequest? Authorized => Validation.Value;
    }

    public async Task<Binding> Bind(HttpRequest request)
    {
        var bound = await AuthorizationRequestBinder.BindAsync(request);

        if (!string.IsNullOrEmpty(bound.RequestUri))
        {
            // Un cliente que anuncia PAR llega al authorize con solo client_id y request_uri: sin
            // resolverlo, el authorize no veria redirect_uri ni response_type y rechazaria a un cliente
            // bien configurado.
            var resolved = pushedRequests.Find(bound.RequestUri);

            // Un request_uri desconocido o caducado es invalid_request_uri (RFC 9126 4.1), no un
            // invalid_request generico: el cliente puede distinguirlo y reintentar empujando la peticion.
            if (resolved.Failed)
            {
                return new Binding(
                    AuthorizationValidationResult.Failed(
                        AuthorizationErrors.InvalidRequestUri(resolved.Error!.Description)),
                    bound);
            }

            bound = resolved.Value!;
        }

        return new Binding(validator.Validate(bound), bound);
    }

    /// <summary>
    /// Invalida la peticion empujada que origino esta autorizacion. Va aqui porque el binder es quien
    /// sabe resolver un <c>request_uri</c>: el flujo de authorize no habla con el store de pendientes.
    /// </summary>
    public void ConsumeRequestUri(string requestUri) => pushedRequests.Consume(requestUri);
}
