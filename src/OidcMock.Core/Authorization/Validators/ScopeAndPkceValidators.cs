using OidcMock.Core.Codes;
using OidcMock.Core.Errors;
using OidcMock.Core.Scopes;

namespace OidcMock.Core.Authorization.Validators;

/// <summary>Regla: cada scope solicitado tiene que estar permitido para el cliente.</summary>
public sealed class AllowedScopesValidator : IAuthorizeRequestValidator
{
    public bool ErrorIsRedirectable => true;

    public ProtocolError? Validate(AuthorizeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Client is not null && context.Request.EffectiveScopes.All(context.Client.AllowsScope)
            ? null
            : ProtocolErrors.InvalidScope("Uno de los scopes solicitados no esta permitido para el cliente.");
    }
}

/// <summary>
/// Regla: ademas de estar permitido, el scope tiene que existir en scopes.json. Si no, el token
/// resultante llevaria un scope que el mock no sabe proyectar a claims.
/// </summary>
public sealed class KnownScopesValidator(IScopeStore scopeStore) : IAuthorizeRequestValidator
{
    public bool ErrorIsRedirectable => true;

    public ProtocolError? Validate(AuthorizeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scopeStore);

        return context.Request.EffectiveScopes.Any(scope => scopeStore.Find(scope) is null)
            ? ProtocolErrors.InvalidScope("Uno de los scopes solicitados no existe en la configuracion del mock.")
            : null;
    }
}

/// <summary>
/// Regla: la peticion debe traer el scope openid, que es lo que la convierte en una peticion OIDC.
/// Sin el, el authorization endpoint se usaria como un OAuth plano y el id_token no tendria sentido.
/// </summary>
public sealed class OpenIdScopeValidator : IAuthorizeRequestValidator
{
    public bool ErrorIsRedirectable => true;

    public ProtocolError? Validate(AuthorizeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.EffectiveScopes.Contains(ScopeNames.OpenId, StringComparer.Ordinal)
            ? null
            : ProtocolErrors.InvalidScope($"El scope '{ScopeNames.OpenId}' es obligatorio en /connect/authorize.");
    }
}

/// <summary>Regla: si el cliente exige PKCE, la peticion tiene que traer code_challenge.</summary>
public sealed class PkceRequiredValidator : IAuthorizeRequestValidator
{
    public bool ErrorIsRedirectable => true;

    public ProtocolError? Validate(AuthorizeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Client is null)
        {
            return null;
        }

        return context.Client.RequirePkce && string.IsNullOrEmpty(context.Request.CodeChallenge)
            ? ProtocolErrors.InvalidRequest($"El cliente '{context.Client.ClientId}' requiere PKCE.")
            : null;
    }
}

/// <summary>Regla: el code_challenge_method solo puede ser plain o S256 (RFC 7636 4.3).</summary>
public sealed class CodeChallengeMethodValidator : IAuthorizeRequestValidator
{
    public bool ErrorIsRedirectable => true;

    public ProtocolError? Validate(AuthorizeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;

        // Un parametro vacio o en blanco equivale a no enviarlo: asi es como un formulario
        // reenvia los campos que el cliente no relleno, y el default de RFC 7636 4.3 es plain.
        var hasMethod = !string.IsNullOrWhiteSpace(request.CodeChallengeMethod);

        if (hasMethod && !CodeChallenges.IsSupported(request.CodeChallengeMethod!))
        {
            return ProtocolErrors.InvalidRequest(
                $"El code_challenge_method '{request.CodeChallengeMethod}' no esta soportado.");
        }

        return hasMethod && string.IsNullOrEmpty(request.CodeChallenge)
            ? ProtocolErrors.InvalidRequest("El code_challenge_method requiere un code_challenge.")
            : null;
    }
}
