namespace OidcMock.Core.Users;

/// <summary>
/// Identidades sinteticas creadas desde la pantalla del mock: el usuario escribe el subject y los
/// claims que quiere en el JWT, sin credenciales. Viven en memoria y sombrean a las de users.json.
/// </summary>
public interface IUserOverlay
{
    void Remember(User user);
}
