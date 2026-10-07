namespace OidcMock.Core.Users;

/// <summary>
/// Valida las credenciales de un usuario contra el store. Vive aparte del caso de uso del
/// authorize porque la ruta de entrada /Account/Login del servidor real autentica sin peticion de
/// autorizacion: solo usuario, contrasena y una sesion que abrir. La comparacion es en tiempo
/// constante para no filtrar informacion por temporizacion.
/// </summary>
public sealed class UserAuthenticator(IUserStore users)
{
    public User? Authenticate(string userName, string password)
    {
        ArgumentNullException.ThrowIfNull(users);

        var user = users.FindByUserName(userName);

        return user is not null && CredentialsMatch(user, password) ? user : null;
    }

    private static bool CredentialsMatch(User user, string password) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(user.Password),
            System.Text.Encoding.UTF8.GetBytes(password ?? string.Empty));
}