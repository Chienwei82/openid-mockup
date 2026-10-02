namespace OidcMock.Core.Configuration;

/// <summary>
/// Un problema de configuracion: que opcion esta mal y por que. Se acumulan en lugar de fallar en
/// la primera regla, para que un arranque con la configuracion rota diga todo lo que esta mal de una vez.
/// </summary>
public sealed record OptionValidationError(string Setting, string Reason)
{
    public override string ToString() => $"{Setting}: {Reason}";
}

/// <summary>
/// Reglas de coherencia de <see cref="OidcMockOptions"/>. Vive en Core y no depende de ASP.NET para
/// poder probarse como dominio puro; el adaptador que la conecta con el arranque valido esta en el Host.
/// </summary>
public static class OidcMockOptionsValidator
{
    private const string SectionName = "OidcMock";
    private const int MaximumPort = 65535;

    /// <summary>
    /// Devuelve todos los problemas de las opciones dadas, o una lista vacia si son utilizables.
    /// </summary>
    public static IReadOnlyList<OptionValidationError> Validate(OidcMockOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<OptionValidationError> errors = [];

        CheckPathBase(options, errors);
        CheckIssuer(options, errors);
        CheckSessionLifetime(options, errors);
        CheckCorsOrigins(options, errors);
        CheckServing(options, errors);

        return errors;
    }

    private static void CheckPathBase(OidcMockOptions options, List<OptionValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(options.PathBase))
        {
            errors.Add(new OptionValidationError($"{SectionName}:PathBase", "no puede estar vacio."));
        }
        else if (!options.PathBase.StartsWith('/'))
        {
            errors.Add(new OptionValidationError(
                $"{SectionName}:PathBase",
                $"debe empezar por '/', pero es '{options.PathBase}'."));
        }
    }

    private static void CheckIssuer(OidcMockOptions options, List<OptionValidationError> errors)
    {
        // Null significa "deducirlo del host de la peticion", que siempre es una opcion valida.
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            return;
        }

        if (!Uri.TryCreate(options.Issuer, UriKind.Absolute, out var issuer))
        {
            errors.Add(new OptionValidationError(
                $"{SectionName}:Issuer",
                $"debe ser una URL absoluta, pero es '{options.Issuer}'."));
            return;
        }

        if (issuer.Scheme is not ("http" or "https"))
        {
            errors.Add(new OptionValidationError(
                $"{SectionName}:Issuer",
                $"solo admite http o https, pero es '{issuer.Scheme}'."));
        }
    }

    private static void CheckSessionLifetime(OidcMockOptions options, List<OptionValidationError> errors)
    {
        if (options.SessionLifetime <= TimeSpan.Zero)
        {
            errors.Add(new OptionValidationError(
                $"{SectionName}:SessionLifetime",
                $"debe ser positiva, pero es {options.SessionLifetime}."));
        }
    }

    /// <summary>
    /// Cada origen tiene que ser un origen real (esquema, host y puerto opcional, sin ruta): un
    /// comodin se rechaza a proposito, porque el token endpoint admite credenciales y un
    /// <c>Access-Control-Allow-Origin: *</c> con credenciales es invalido en los navegadores.
    /// </summary>
    private static void CheckCorsOrigins(OidcMockOptions options, List<OptionValidationError> errors)
    {
        for (var index = 0; index < options.AllowedCorsOrigins.Count; index++)
        {
            var origin = options.AllowedCorsOrigins[index];
            var setting = $"{SectionName}:AllowedCorsOrigins:{index}";

            if (string.IsNullOrWhiteSpace(origin))
            {
                errors.Add(new OptionValidationError(setting, "no puede estar vacio."));
            }
            else if (origin == "*")
            {
                errors.Add(new OptionValidationError(setting, "no se admite el comodin; enumera los origenes."));
            }
            else if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https") ||
                string.IsNullOrEmpty(uri.Host) ||
                (uri.AbsolutePath.Length > 1 && uri.AbsolutePath != "/") ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                errors.Add(new OptionValidationError(
                    setting,
                    $"debe ser un origen sin ruta ni query (esquema://host[:puerto]), pero es '{origin}'."));
            }
        }
    }

    private static void CheckServing(OidcMockOptions options, List<OptionValidationError> errors)
    {
        var serving = options.Serving;

        if (!serving.UseHttps && !serving.AllowHttp)
        {
            errors.Add(new OptionValidationError(
                $"{SectionName}:Serving",
                "no escucha en ningun esquema: deja UseHttps o AllowHttp encendido."));
        }

        CheckPort(errors, $"{SectionName}:Serving:HttpsPort", serving.HttpsPort, serving.UseHttps);
        CheckPort(errors, $"{SectionName}:Serving:HttpPort", serving.HttpPort, serving.AllowHttp);
    }

    /// <summary>
    /// Solo se valida el puerto que se va a usar: un puerto invalido en el esquema apagado no impide
    /// arrancar, porque no se escucha en el.
    /// </summary>
    private static void CheckPort(
        List<OptionValidationError> errors,
        string setting,
        int port,
        bool enabled)
    {
        if (enabled && (port is < 1 || port > MaximumPort))
        {
            errors.Add(new OptionValidationError(setting, $"debe estar entre 1 y {MaximumPort}, pero es {port}."));
        }
    }
}