using System.Text.RegularExpressions;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Lee los campos del formulario de las pantallas del mock para enviarlos tal cual lo haria el
/// navegador: los ocultos (la peticion de autorizacion viaja en ellos) y los visibles con su valor
/// precargado (subject y claims editables). No intenta ser un parser de HTML general.
/// </summary>
public static partial class LoginFormFields
{
    public static Dictionary<string, string> Parse(string html)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match match in InputPattern().Matches(html))
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
        """<input[^>]*name=["'](?<name>[^"']+)["'][^>]*value=["'](?<value>[^"']*)["']""",
        RegexOptions.IgnoreCase)]
    private static partial Regex InputPattern();
}