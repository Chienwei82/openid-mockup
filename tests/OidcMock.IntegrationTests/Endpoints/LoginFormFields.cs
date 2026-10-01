using System.Text.RegularExpressions;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Lee los campos ocultos que el mock renderiza en la pantalla de login, para poder enviarlos tal
/// cual lo haria el navegador. El mock los emite uno por input hidden, que es lo unico que la
/// prueba necesita; no intenta ser un parser de HTML general.
/// </summary>
public static partial class LoginFormFields
{
    public static Dictionary<string, string> Parse(string html)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match match in HiddenInputPattern().Matches(html))
        {
            fields[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        if (fields.Count == 0)
        {
            throw new InvalidOperationException("La pantalla de login no traia los campos del formulario.");
        }

        return fields;
    }

    [GeneratedRegex(
        """<input[^>]*type=["']hidden["'][^>]*name=["'](?<name>[^"']+)["'][^>]*value=["'](?<value>[^"']*)["']""",
        RegexOptions.IgnoreCase)]
    private static partial Regex HiddenInputPattern();
}