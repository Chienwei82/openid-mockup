using System.Text.Encodings.Web;
using System.Text.Json;
using OidcMock.Core.Users;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Campos editables de la identidad del mock: los enlaces de perfil que recargan los valores de un
/// usuario de users.json, el subject y un input por claim del perfil precargado. Sin JavaScript: el
/// perfil se elige con enlaces GET y los valores viajan en el POST. Lo comparten la pantalla del
/// authorize y la de /Account/Login.
/// </summary>
public static class IdentityFields
{
    public static string Render(User profile, IReadOnlyList<User> profiles, Func<string, string> perfilHref)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(perfilHref);

        return $"""
            {PerfilLinks(profiles, perfilHref)}
            <label for="{LoginFormFields.Subject}">Subject</label>
            <input id="{LoginFormFields.Subject}" name="{LoginFormFields.Subject}" value="{Escape(profile.Subject)}" required />
            {string.Join(Environment.NewLine, profile.Claims.Select(ClaimInput))}
            """;
    }

    private static string PerfilLinks(IReadOnlyList<User> profiles, Func<string, string> perfilHref) =>
        $"""
            <nav class="perfiles">Perfiles: {string.Join(" · ", profiles.Select(user => PerfilLink(user.UserName, perfilHref)))}</nav>
            """;

    private static string PerfilLink(string userName, Func<string, string> perfilHref) =>
        $"""<a href="{Escape(perfilHref(userName))}">{Escape(userName)}</a>""";

    private static string ClaimInput(KeyValuePair<string, JsonElement> claim) =>
        $"""
            <label for="{Escape($"{LoginFormFields.ClaimPrefix}{claim.Key}")}">{Escape(claim.Key)}</label>
            <input id="{Escape($"{LoginFormFields.ClaimPrefix}{claim.Key}")}" name="{Escape($"{LoginFormFields.ClaimPrefix}{claim.Key}")}" value="{Escape(DisplayValue(claim.Value))}" />
            """;

    /// <summary>
    /// Las cadenas se editan sin comillas; los demas valores viajan como JSON compacto, que es lo
    /// que ParseValue devuelve a su tipo original al recibir el formulario.
    /// </summary>
    private static string DisplayValue(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();

    private static string Escape(string value) => HtmlEncoder.Default.Encode(value);
}