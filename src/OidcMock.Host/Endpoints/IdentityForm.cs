using System.Text.Json;
using OidcMock.Core.Users;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Construye la identidad que el usuario edito en la pantalla del mock: el perfil precargado, los
/// valores de los campos de claims y el subject tecleado. Los valores que parsean como JSON
/// conservan su tipo (true, 123, ["rol"]), el resto viaja como cadena. Un campo vacio se omite:
/// borrar un claim es quitarlo del JWT. Si el formulario no trae ningun campo de claims se usa el
/// perfil precargado tal cual, para que un POST que solo trae la peticion de autorizacion conceda
/// con la identidad por defecto.
/// </summary>
public static class IdentityForm
{
    private const string NoProfiles = "users.json no tiene usuarios y la pantalla de identidad no tiene nada que precargar.";

    public static User ReadUser(IReadOnlyDictionary<string, string> form, IReadOnlyList<User> profiles)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(profiles);

        var fallback = BaseProfile(form.GetValueOrDefault(LoginFormFields.Perfil), profiles);
        var subject = form.GetValueOrDefault(LoginFormFields.Subject) is { Length: > 0 } typed
            ? typed
            : fallback.Subject;

        return new User(subject, subject, string.Empty, Claims(form, fallback));
    }

    /// <summary>
    /// El perfil base de la pantalla: el elegido por su username (o subject) en el campo "perfil" y,
    /// sin eleccion, el primero de users.json. Se acepta el subject porque la sesion recuerda la
    /// identidad por el, y select_account precarga la cuenta de la sesion.
    /// </summary>
    public static User BaseProfile(string? perfil, IReadOnlyList<User> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var chosen = profiles.FirstOrDefault(user =>
            string.Equals(user.UserName, perfil, StringComparison.Ordinal)
            || string.Equals(user.Subject, perfil, StringComparison.Ordinal));

        return chosen
            ?? (profiles.Count > 0 ? profiles[0] : throw new InvalidOperationException(NoProfiles));
    }

    /// <summary>
    /// Solo Denegar deniega. El boton de Aceptar manda action=accept, pero un POST programatico que
    /// no trae action concede: los scripts que no pasan por la pantalla esperan obtener su codigo.
    /// </summary>
    public static bool IsDenied(IReadOnlyDictionary<string, string> form)
    {
        ArgumentNullException.ThrowIfNull(form);

        return string.Equals(form.GetValueOrDefault(LoginFormFields.Action), LoginFormFields.Deny, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, JsonElement> Claims(IReadOnlyDictionary<string, string> form, User fallback) =>
        form.Keys.Any(key => key.StartsWith(LoginFormFields.ClaimPrefix, StringComparison.Ordinal))
            ? ReadClaims(form)
            : fallback.Claims;

    private static Dictionary<string, JsonElement> ReadClaims(IReadOnlyDictionary<string, string> form) =>
        form
            .Where(field => field.Key.StartsWith(LoginFormFields.ClaimPrefix, StringComparison.Ordinal))
            .Where(field => field.Value.Length > 0)
            .ToDictionary(
                field => field.Key[LoginFormFields.ClaimPrefix.Length..],
                field => ParseValue(field.Value),
                StringComparer.Ordinal);

    private static JsonElement ParseValue(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(value);
        }
    }
}