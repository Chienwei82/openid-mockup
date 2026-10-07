using OidcMock.Core.Errors;

namespace OidcMock.Core.Authorization.Validators;

/// <summary>
/// Regla: el response_type tiene que ser una combinacion que el endpoint sepa responder.
/// Valida contra <see cref="ResponseTypeNames.EmittedByAuthorizationEndpoint"/> y no contra la lista
/// que anuncia el discovery: son dos listas distintas a proposito (D-042).
/// </summary>
public sealed class ResponseTypeValidator : IAuthorizeRequestValidator
{
    public bool ErrorIsRedirectable => true;

    public ProtocolError? Validate(AuthorizeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ResponseTypeNames.EmittedByAuthorizationEndpoint.Contains(
            context.Request.ResponseType ?? string.Empty,
            StringComparer.Ordinal)
            ? null
            : ProtocolErrors.UnsupportedResponseType(
                $"El response_type '{context.Request.ResponseType}' no esta soportado.");
    }
}

/// <summary>
/// Regla: el response_mode tiene que ser query, fragment o form_post, y el query solo vale cuando
/// la respuesta es un codigo de autorizacion. Los tokens no viajan en el query string (Multi
/// Response Type Encoding 3): un id_token en el query se filtra en historiales y cabeceras.
/// </summary>
public sealed class ResponseModeValidator : IAuthorizeRequestValidator
{
    public bool ErrorIsRedirectable => true;

    public ProtocolError? Validate(AuthorizeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ResponseModes.Supported.Contains(context.Request.ResponseMode, StringComparer.Ordinal))
        {
            return ProtocolErrors.InvalidRequest($"El response_mode '{context.Request.ResponseMode}' no esta soportado.");
        }

        return string.Equals(context.Request.ResponseMode, ResponseModes.Query, StringComparison.Ordinal)
            && !string.Equals(context.Request.ResponseType, ResponseTypeNames.Code, StringComparison.Ordinal)
            ? ProtocolErrors.InvalidRequest("El response_mode 'query' no admite tokens en la respuesta.")
            : null;
    }
}

/// <summary>Regla: el prompt tiene que estar entre los que anuncia prompt_values_supported.</summary>
public sealed class PromptValidator : IAuthorizeRequestValidator
{
    public bool ErrorIsRedirectable => true;

    public ProtocolError? Validate(AuthorizeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var prompt = context.Request.Prompt;

        return string.IsNullOrWhiteSpace(prompt) || PromptValues.Supported.Contains(prompt, StringComparer.Ordinal)
            ? null
            : ProtocolErrors.InvalidRequest($"El prompt '{prompt}' no esta soportado.");
    }
}

/// <summary>Regla: el grant_type de la peticion tiene que estar permitido para el cliente.</summary>
public sealed class GrantTypeValidator : IAuthorizeRequestValidator
{
    public bool ErrorIsRedirectable => true;

    public ProtocolError? Validate(AuthorizeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Client is null)
        {
            return null;
        }

        return context.Client.AllowsGrantType(context.Request.GrantType ?? string.Empty)
            ? null
            : ProtocolErrors.UnauthorizedClient(
                $"El cliente '{context.Client.ClientId}' no puede usar el grant_type '{context.Request.GrantType}'.");
    }
}
