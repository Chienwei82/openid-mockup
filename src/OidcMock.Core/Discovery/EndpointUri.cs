namespace OidcMock.Core.Discovery;

/// <summary>
/// Normaliza el PathBase y el issuer, y compone las URLs que anuncia el discovery
/// sin concatenar cadenas a mano en los endpoints.
/// </summary>
public static class EndpointUri
{
    public static string NormalizePathBase(string? pathBase)
    {
        var trimmed = (pathBase ?? string.Empty).Trim().Trim('/');

        return trimmed.Length == 0 ? "/" : $"/{trimmed}";
    }

    public static string NormalizeIssuer(string? issuer)
    {
        var trimmed = (issuer ?? string.Empty).Trim().TrimEnd('/');

        return $"{trimmed}/";
    }

    public static string Combine(string normalizedIssuer, string relativePath) =>
        $"{normalizedIssuer.TrimEnd('/')}/{relativePath.TrimStart('/')}";
}
