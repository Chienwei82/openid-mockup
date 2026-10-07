namespace OidcMock.Host.Endpoints;

/// <summary>
/// Nombres de los campos del formulario de identidad del mock: el subject que el usuario escribe y
/// los valores de claims editables que viajaran en el JWT. El campo de accion decide entre aceptar
/// (conceder) y denegar (simular un fallo de autenticacion).
/// </summary>
public static class LoginFormFields
{
    public const string Subject = "sub";
    public const string ClaimPrefix = "claim.";
    public const string Action = "action";
    public const string Accept = "accept";
    public const string Deny = "deny";
    public const string Perfil = "perfil";
}
