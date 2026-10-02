using OidcMock.Core.Errors;

namespace OidcMock.Core.Authorization.Validators;

/// <summary>Regla: el client_id de la peticion debe corresponder a un cliente registrado.</summary>
public sealed class ClientExistsValidator : IAuthorizeRequestValidator
{
    public bool ErrorIsRedirectable => false;

    public ProtocolError? Validate(AuthorizeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Client is null
            ? AuthorizationErrors.InvalidClient($"El cliente '{context.Request.ClientId}' no esta registrado.")
            : null;
    }
}

/// <summary>
/// Regla: el redirect_uri tiene que estar registrado para ese cliente. La comparacion es exacta,
/// sin tolerar prefijos ni rutas: es la unica garantia de que la redireccion no es un open redirect.
/// </summary>
public sealed class RedirectUriValidator : IAuthorizeRequestValidator
{
    public bool ErrorIsRedirectable => false;

    public ProtocolError? Validate(AuthorizeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Sin cliente no hay lista de redirect_uri contra la que comparar.
        if (context.Client is null)
        {
            return null;
        }

        return context.Client.AllowsRedirectUri(context.Request.RedirectUri ?? string.Empty)
            ? null
            : ProtocolErrors.InvalidRequest(
                $"El redirect_uri '{context.Request.RedirectUri}' no esta registrado para el cliente.");
    }
}
