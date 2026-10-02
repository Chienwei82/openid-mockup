namespace OidcMock.Host.Endpoints;

/// <summary>
/// Nombres de los campos del formulario de login. "user" es el campo con la lista desplegable de
/// users.json y "username" el de texto libre; el endpoint acepta ambos para no obligar a elegir.
/// </summary>
public static class LoginFormFields
{
    public const string User = "user";
    public const string UserName = "username";
    public const string Password = "password";
}
