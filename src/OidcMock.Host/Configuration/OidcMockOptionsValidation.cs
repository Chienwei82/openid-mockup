using Microsoft.Extensions.Options;
using OidcMock.Core.Configuration;

namespace OidcMock.Host.Configuration;

/// <summary>
/// Conecta <see cref="OidcMockOptionsValidator"/> con el arranque: al resolver las opciones se
/// lanzan todos los problemas de una vez, en vez de fallar mas tarde en la primera peticion.
/// </summary>
internal sealed class OidcMockOptionsValidation : IValidateOptions<OidcMockOptions>
{
    public ValidateOptionsResult Validate(string? name, OidcMockOptions options)
    {
        var errors = OidcMockOptionsValidator.Validate(options);

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors.Select(error => error.ToString()));
    }
}